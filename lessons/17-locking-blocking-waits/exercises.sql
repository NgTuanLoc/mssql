USE AdventureWorks2022;
GO
-- Several exercises need TWO sessions. Open a second terminal: .\scripts\connect.ps1

-- Exercise 1: Create a blocking situation.
--   Session A: BEGIN TRAN; UPDATE lesson17.Account SET Balance = Balance - 50 WHERE AccountID = 2; (leave open)
--   Session B: SELECT * FROM lesson17.Account WHERE AccountID = 2;  (this blocks)
-- Then, from a third session (or Session A in a new batch), write a query that reports the
-- waiting session, who is blocking it, and the wait_type.
-- Your diagnostic query here:


-- Exercise 2: While the block from Exercise 1 is active, list the locks held by Session A,
--             showing resource_type and request_mode. Identify the exclusive (X) lock.
-- Your query here:  (remember to COMMIT Session A afterwards)


-- Exercise 3: Run a large UPDATE on lesson17.BigOrders inside a transaction and show, from
--             another session, that lock escalation produced an OBJECT-level lock.
--             ROLLBACK afterwards.
-- Your statements here:


-- Exercise 4: Produce a deadlock using two sessions and the AccountID 1 / 2 ordering from the
--             examples. Then query the system_health ring buffer for the deadlock graph.
-- Your statements + query here:


-- Exercise 5: Show the top 5 non-idle cumulative wait types. In a comment, name what each of
--             your top 2 waits generally indicates.
-- Your query + comment here:
