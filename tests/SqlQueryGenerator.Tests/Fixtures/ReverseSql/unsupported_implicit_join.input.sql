SELECT orders.id,
       customers.name
FROM orders,
     customers
WHERE orders.customer_id = customers.id
