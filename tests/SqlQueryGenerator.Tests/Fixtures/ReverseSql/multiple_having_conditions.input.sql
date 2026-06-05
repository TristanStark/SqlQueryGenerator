SELECT orders.customer_id,
       COUNT(orders.id) AS order_count,
       SUM(orders.amount) AS total_amount
FROM orders
GROUP BY orders.customer_id
HAVING order_count > 1
   AND total_amount >= 100
ORDER BY total_amount DESC
