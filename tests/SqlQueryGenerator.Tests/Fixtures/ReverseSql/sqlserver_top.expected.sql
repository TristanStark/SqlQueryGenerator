SELECT orders.id, orders.status FROM orders WHERE orders.status = @status ORDER BY orders.id DESC LIMIT 5
