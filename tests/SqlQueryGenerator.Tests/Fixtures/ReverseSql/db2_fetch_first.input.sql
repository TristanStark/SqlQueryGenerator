SELECT customers.id,
       customers.name
FROM customers
ORDER BY customers.name
FETCH FIRST 10 ROWS ONLY
