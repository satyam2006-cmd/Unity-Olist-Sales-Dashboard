# Olist dashboard data source

The dashboard uses the Brazilian E-Commerce Public Dataset by Olist, published on Kaggle:

- https://www.kaggle.com/datasets/olistbr/brazilian-ecommerce
- License stated by the dataset mirror: Creative Commons Attribution-NonCommercial-ShareAlike 4.0 (CC BY-NC-SA 4.0).
- Coverage: approximately 99,000 orders from 2016–2018.

`OlistRaw/` contains the compressed source tables used for the build. Run `python Datasets/prepare_olist.py` to join them into `Assets/Resources/OlistSales.csv`, the order-line file loaded by Unity.

Dashboard metrics use item price as revenue, excluding freight. Delivered orders are counted from the source `order_status == delivered` field. Olist has no profit field, so the dashboard does not report profit.
