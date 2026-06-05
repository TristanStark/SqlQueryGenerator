SELECT customers.id, customers.name FROM customers WHERE customers.name ILIKE :pattern ORDER BY customers.name ASC
