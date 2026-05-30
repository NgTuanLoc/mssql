USE AdventureWorks2022;
GO
-- XML data type methods (used by the Exercise 4 deadlock-graph query) require QUOTED_IDENTIFIER ON.
SET QUOTED_IDENTIFIER ON;
GO

-- Exercise 1: Diagnose blocking.
-- Approach: sys.dm_exec_requests exposes blocking_session_id for waiting requests.
SELECT r.session_id AS WaitingSession, r.blocking_session_id AS BlockedBy,
       r.wait_type, r.wait_resource, t.text AS WaitingSQL
FROM sys.dm_exec_requests AS r
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) AS t
WHERE r.blocking_session_id <> 0;
-- The waiting session is Session B; BlockedBy is Session A's SPID; wait_type is typically LCK_M_S
-- (it wants a Shared lock that Session A's Exclusive lock is holding off).

-- Exercise 2: Locks held by the blocker.
-- Approach: sys.dm_tran_locks; the X lock on the KEY/RID is the one causing the block.
SELECT request_session_id, resource_type, request_mode, request_status,
       OBJECT_NAME(p.object_id) AS ObjectName
FROM sys.dm_tran_locks AS l
LEFT JOIN sys.partitions AS p ON p.hobt_id = l.resource_associated_entity_id
WHERE resource_database_id = DB_ID()
ORDER BY request_session_id, resource_type;
-- Look for request_mode = 'X' on a KEY (or RID) resource held by Session A.
-- Remember to COMMIT/ROLLBACK Session A to clear the block.

-- Exercise 3: Lock escalation.
-- Approach: an UPDATE touching every row escalates to a single OBJECT (table) X lock.
BEGIN TRANSACTION;
UPDATE lesson17.BigOrders SET Status = Status WHERE SalesOrderID > 0;
-- In another session, for this SPID:
--   SELECT resource_type, request_mode FROM sys.dm_tran_locks WHERE request_session_id = <SPID>;
--   Expect an 'OBJECT' resource_type with request_mode 'X' (escalated from many KEY locks).
ROLLBACK TRANSACTION;

-- Exercise 4: Deadlock + read the graph.
-- Approach: two sessions lock rows in opposite order; one is chosen as victim (Msg 1205).
-- Run the A/B ordering from examples.sql, then read the graph:
SELECT XEvent.value('(@timestamp)[1]', 'datetime2') AS DeadlockTime,
       XEvent.query('.')                            AS DeadlockGraphXml
FROM (
    SELECT CAST(target_data AS XML) AS TargetData
    FROM sys.dm_xe_session_targets st
    JOIN sys.dm_xe_sessions s ON s.address = st.event_session_address
    WHERE s.name = 'system_health' AND st.target_name = 'ring_buffer'
) AS Data
CROSS APPLY TargetData.nodes('RingBufferTarget/event[@name="xml_deadlock_report"]') AS X(XEvent)
ORDER BY DeadlockTime DESC;
-- The XML shows both processes, the resources each held and wanted, and which was the victim.

-- Exercise 5: Top waits.
-- Approach: aggregate sys.dm_os_wait_stats, filtering idle waits.
SELECT TOP 5 wait_type, wait_time_ms, waiting_tasks_count
FROM sys.dm_os_wait_stats
WHERE wait_type NOT IN ('SLEEP_TASK','LAZYWRITER_SLEEP','WAITFOR','XE_TIMER_EVENT',
        'REQUEST_FOR_DEADLOCK_SEARCH','CHECKPOINT_QUEUE','DIRTY_PAGE_POLL','BROKER_TASK_STOP',
        'SOS_WORK_DISPATCHER','LOGMGR_QUEUE','QDS_PERSIST_TASK_MAIN_LOOP_SLEEP',
        'DISPATCHER_QUEUE_SEMAPHORE','SP_SERVER_DIAGNOSTICS_SLEEP','BROKER_TO_FLUSH',
        'PWAIT_EXTENSIBILITY_CLEANUP_TASK','QDS_ASYNC_QUEUE')
ORDER BY wait_time_ms DESC;
-- Common interpretations:
--   PAGEIOLATCH_* -> waiting on data pages from disk (I/O or memory pressure).
--   LCK_M_*       -> blocking/locking contention.
--   CXPACKET/CXCONSUMER -> parallelism coordination (often benign; investigate if dominant).
--   WRITELOG      -> transaction log write latency.
