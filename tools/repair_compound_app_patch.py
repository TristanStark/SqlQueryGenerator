from pathlib import Path

path = Path(__file__).resolve().parent / "apply_compound_app_tests_patch.py"
content = path.read_text(encoding="utf-8")
old = '''        vm.RawSqlText = """
            SELECT CUSTOMER.ID
            FROM CUSTOMER
            UNION ALL
            SELECT ORDERS.CUSTOMER_ID
            FROM ORDERS
            WHERE ORDERS.STATUS = :status
            ORDER BY ID
            """;'''
new = '''        vm.RawSqlText = @"\n            SELECT CUSTOMER.ID\n            FROM CUSTOMER\n            UNION ALL\n            SELECT ORDERS.CUSTOMER_ID\n            FROM ORDERS\n            WHERE ORDERS.STATUS = :status\n            ORDER BY ID\n            ";'''
if old not in content:
    raise RuntimeError("Nested C# multiline string block was not found in app patch script.")
path.write_text(content.replace(old, new, 1), encoding="utf-8", newline="\n")
print("App patch script repaired.")
