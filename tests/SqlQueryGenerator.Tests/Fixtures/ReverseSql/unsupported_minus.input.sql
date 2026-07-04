SELECT pnj.id
FROM pnj
MINUS
SELECT archived_pnj.id
FROM archived_pnj
