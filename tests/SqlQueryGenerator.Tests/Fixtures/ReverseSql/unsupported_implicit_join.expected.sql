SELECT orders.id, customers.name FROM orders INNER JOIN customers ON orders.customer_id = customers.id
