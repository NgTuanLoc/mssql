USE AdventureWorks2022;
GO

-- Exercise 1: List every product with its subcategory name.
--             Include products that have no subcategory (NULL subcategory).
-- Expected columns: ProductID, ProductName, SubcategoryName
-- Tables: Production.Product, Production.ProductSubcategory
-- Your query here:


-- Exercise 2: List every sales order with the customer's full name.
--             Only include orders where a matching person record exists.
-- Expected columns: SalesOrderID, OrderDate, CustomerFullName
-- Tables: Sales.SalesOrderHeader, Sales.Customer, Person.Person
-- Your query here:


-- Exercise 3: Find all employees who have NEVER placed a purchase order.
-- Expected columns: BusinessEntityID, JobTitle
-- Tables: HumanResources.Employee, Purchasing.PurchaseOrderHeader
-- Hint: use a LEFT JOIN checking for NULL, or NOT EXISTS / EXCEPT.
-- Your query here:


-- Exercise 4: Show the CROSS JOIN of lesson04.Left and lesson04.Right.
--             How many rows do you expect?
-- Expected columns: LeftID, LeftValue, RightID, RightValue
-- Your query here:


-- Exercise 5: Using UNION ALL, combine the first names of all people
--             (Person.Person) with all contact names in the Vendor table
--             (Purchasing.Vendor, column: Name). Label which table each row
--             comes from.
-- Expected columns: Name, Source
-- Your query here:
