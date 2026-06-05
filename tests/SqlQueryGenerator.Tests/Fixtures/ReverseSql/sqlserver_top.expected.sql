SELECT o.id, o.status FROM orders o WHERE o.status = @status ORDER BY o.id DESC LIMIT 5
