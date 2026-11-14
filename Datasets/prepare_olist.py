"""Join the public Olist source tables into the compact rows used by Unity."""
import csv
import gzip
from pathlib import Path

ROOT = Path(__file__).resolve().parent
RAW = ROOT / "OlistRaw"
OUTPUT = Path(__file__).resolve().parents[1] / "LiveData" / "OlistSales.csv"


def read_gzip(name):
    with gzip.open(RAW / name, "rt", encoding="utf-8-sig", newline="") as source:
        return list(csv.DictReader(source))


orders = {
    row["order_id"]: row
    for row in read_gzip("olist_orders_dataset.csv.gz")
}
customers = {
    row["customer_id"]: row
    for row in read_gzip("olist_customers_dataset.csv.gz")
}
products = {
    row["product_id"]: row
    for row in read_gzip("olist_products_dataset.csv.gz")
}
translations = {
    row["product_category_name"]: row["product_category_name_english"]
    for row in read_gzip("product_category_name_translation.csv.gz")
}

OUTPUT.parent.mkdir(parents=True, exist_ok=True)
items = read_gzip("olist_order_items_dataset.csv.gz")
seen_orders = set()
headers = [
    "Order ID", "Order Date", "Ship Date", "Customer ID", "Region",
    "Category", "Sales", "Quantity", "City", "Sub-Category", "Order Status",
]
with OUTPUT.open("w", encoding="utf-8", newline="") as target:
    writer = csv.DictWriter(target, fieldnames=headers)
    writer.writeheader()
    for item in items:
        order = orders[item["order_id"]]
        customer = customers.get(order["customer_id"], {})
        product = products.get(item["product_id"], {})
        category_key = product.get("product_category_name") or "uncategorized"
        category = translations.get(category_key, category_key)
        category = category.replace("_", " ").title()
        writer.writerow({
            "Order ID": item["order_id"],
            "Order Date": order["order_purchase_timestamp"],
            "Ship Date": order["order_delivered_customer_date"],
            "Customer ID": customer.get("customer_unique_id", order["customer_id"]),
            "Region": customer.get("customer_state", ""),
            "Category": category,
            "Sales": item["price"],
            "Quantity": 1,
            "City": customer.get("customer_city", "Unknown").replace("_", " ").title(),
            "Sub-Category": category,
            "Order Status": order["order_status"],
        })
        seen_orders.add(item["order_id"])

    # Keep orders with no item rows in the order/customer/delivery KPIs.
    for order_id, order in orders.items():
        if order_id in seen_orders:
            continue
        customer = customers.get(order["customer_id"], {})
        writer.writerow({
            "Order ID": order_id,
            "Order Date": order["order_purchase_timestamp"],
            "Ship Date": order["order_delivered_customer_date"],
            "Customer ID": customer.get("customer_unique_id", order["customer_id"]),
            "Region": customer.get("customer_state", ""),
            "Category": "Uncategorized",
            "Sales": 0,
            "Quantity": 0,
            "City": customer.get("customer_city", "Unknown").replace("_", " ").title(),
            "Sub-Category": "Uncategorized",
            "Order Status": order["order_status"],
        })

print(f"Wrote {OUTPUT} from {len(orders):,} orders and {len(items):,} item rows")
