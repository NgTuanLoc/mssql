USE AdventureWorks2022;
GO
-- XML data type methods (used by the deadlock-graph query below) require QUOTED_IDENTIFIER ON.
SET QUOTED_IDENTIFIER ON;
GO

-- =========================================================================
-- BLOCKING (needs TWO sessions)
-- Open a second terminal and run .\scripts\connect.ps1 to get Session B.
-- =========================================================================

-- ---- Session A: start a transaction and hold an exclusive lock (do NOT commit yet) ----
-- BEGIN TRANSACTION;
-- UPDATE lesson17.Account SET Balance = Balance - 100 WHERE AccountID = 1;
-- (leave this open, switch to Session B)

-- ---- Session B: try to read the same row — it BLOCKS waiting for Session A's lock ----
-- SELECT * FROM lesson17.Account WHERE AccountID = 1;   -- hangs until A commits/rolls back

-- ---- Session A (or a third session): see the blocking chain ----
-- Example 1: Who is blocking whom?
SELECT
    r.session_id        AS WaitingSession,
    r.blocking_session_id AS BlockedBy,
    r.wait_type,
    r.wait_time         AS WaitMs,
    r.wait_resource,
    t.text              AS WaitingSQL
FROM sys.dm_exec_requests AS r
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) AS t
WHERE r.blocking_session_id <> 0;

-- Example 2: What locks are currently held / requested?
SELECT
    l.request_session_id  AS SessionId,
    l.resource_type,
    l.request_mode,         -- S, X, U, IS, IX, etc.
    l.request_status,       -- GRANT or WAIT
    OBJECT_NAME(p.object_id) AS ObjectName
FROM sys.dm_tran_locks AS l
LEFT JOIN sys.partitions AS p ON p.hobt_id = l.resource_associated_entity_id
WHERE l.resource_database_id = DB_ID()
ORDER BY l.request_session_id, l.resource_type;

-- Example 3: Waiting tasks (the live view of what is stuck)
SELECT
    wt.session_id,
    wt.wait_type,
    wt.wait_duration_ms,
    wt.blocking_session_id
FROM sys.dm_os_waiting_tasks AS wt
WHERE wt.session_id > 50;   -- skip system sessions

-- ---- Session A: release the lock ----
-- COMMIT TRANSACTION;   -- Session B's SELECT now returns immediately

-- =========================================================================
-- LOCK MODES & ESCALATION
-- =========================================================================

-- Example 4: A large UPDATE escalates many row/page locks to a single TABLE lock
-- Run this and inspect sys.dm_tran_locks (in another session) — you'll see an OBJECT-level X lock.
BEGIN TRANSACTION;
UPDATE lesson17.BigOrders SET Status = Status WHERE SalesOrderID > 0;  -- touches every row
-- In Session B: SELECT resource_type, request_mode FROM sys.dm_tran_locks
--               WHERE request_session_id = <this SPID>;  -- expect an OBJECT X lock (escalated)
ROLLBACK TRANSACTION;

-- =========================================================================
-- DEADLOCK (needs TWO sessions) — deterministic ordering
-- =========================================================================

-- ---- Session A ----
-- BEGIN TRAN;
-- UPDATE lesson17.Account SET Balance = Balance - 1 WHERE AccountID = 1;  -- locks row 1
-- (pause)                                                                  -- then run the next line
-- UPDATE lesson17.Account SET Balance = Balance - 1 WHERE AccountID = 2;  -- wants row 2

-- ---- Session B (run its first UPDATE during A's pause) ----
-- BEGIN TRAN;
-- UPDATE lesson17.Account SET Balance = Balance - 1 WHERE AccountID = 2;  -- locks row 2
-- UPDATE lesson17.Account SET Balance = Balance - 1 WHERE AccountID = 1;  -- wants row 1 -> DEADLOCK

-- One session becomes the victim (Msg 1205) and is rolled back automatically.

-- Example 5: Read the most recent deadlock graphs from the system_health Extended Event session
SELECT
    XEvent.value('(@timestamp)[1]', 'datetime2')           AS DeadlockTime,
    XEvent.query('.')                                       AS DeadlockGraphXml
FROM (
    SELECT CAST(target_data AS XML) AS TargetData
    FROM sys.dm_xe_session_targets st
    JOIN sys.dm_xe_sessions s ON s.address = st.event_session_address
    WHERE s.name = 'system_health' AND st.target_name = 'ring_buffer'
) AS Data
CROSS APPLY TargetData.nodes('RingBufferTarget/event[@name="xml_deadlock_report"]') AS XEventData(XEvent)
ORDER BY DeadlockTime DESC;

-- =========================================================================
-- WAITS-AND-QUEUES METHODOLOGY
-- =========================================================================

-- Example 6: Top cumulative wait types since the last restart (the master triage query)
SELECT TOP 10
    wait_type,
    wait_time_ms,
    waiting_tasks_count,
    wait_time_ms / NULLIF(waiting_tasks_count, 0) AS AvgWaitMs
FROM sys.dm_os_wait_stats
WHERE wait_type NOT IN (   -- filter benign/idle waits
        'SLEEP_TASK','BROKER_TASK_STOP','XE_TIMER_EVENT','CHECKPOINT_QUEUE',
        'LAZYWRITER_SLEEP','REQUEST_FOR_DEADLOCK_SEARCH','SQLTRACE_INCREMENTAL_FLUSH_SLEEP',
        'WAITFOR','DIRTY_PAGE_POLL','HADR_FILESTREAM_IOMGR_IOCOMPLETION','BROKER_RECEIVE_WAITFOR')
ORDER BY wait_time_ms DESC;

-- Example 7: Reset wait stats (dev only) so you can measure waits for a specific workload
-- DBCC SQLPERF('sys.dm_os_wait_stats', CLEAR);
PRINT 'Uncomment the DBCC SQLPERF line to reset wait stats before measuring a workload.';
