SELECT TOP 5 o.id,
       o.status
FROM orders o
WHERE o.status = @status
ORDER BY o.id DESC
