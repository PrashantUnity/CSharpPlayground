user = {
    "id": 10,
    "name": "Prashant",
    "address": {
        "city": "Patna",
        "state": "Bihar"
    },
    "roles": ["Admin", "Developer"]
}

order_ids = [101, 102, 103, 104]
total_amount = 1450.75
is_verified = True

print(f"User {user['name']} has {len(order_ids)} orders.")
