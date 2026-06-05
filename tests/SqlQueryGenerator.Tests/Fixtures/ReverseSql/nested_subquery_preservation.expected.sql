SELECT orders.customer_id FROM orders WHERE orders.customer_id IN ( SELECT customers.id FROM customers WHERE customers.status = 'ACTIVE') ORDER BY orders.customer_id ASC
