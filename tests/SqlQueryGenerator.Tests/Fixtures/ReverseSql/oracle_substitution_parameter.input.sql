SELECT p.id
FROM people p
WHERE p.age >= &1
ORDER BY p.id
