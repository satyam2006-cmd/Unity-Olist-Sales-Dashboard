# Unity Olist Sales Dashboard

An editable 3D sales dashboard built with Unity 6.6. It uses extruded 3D scene geometry for its KPI marks, chart bars, axes, donut segments, labels, and chart panels. The scene is saved in Edit Mode so the objects are available in the Hierarchy.

## Open the project

1. Open this folder in Unity 6.6.7f1.
2. Open `Assets/Scenes/QuakeScopeDashboard.unity`.
3. Press Play to view the dashboard.

To rebuild the saved scene from the prepared dataset, use **Tools → QuakeScope → Build Editable 3D Dashboard**.

## Dashboard contents

- KPI row: unique customers, orders, item revenue, average order revenue, delivered orders, items sold, and items per order.
- Revenue by month, units by category, revenue by product category, and revenue by city.
- Clickable KPIs and chart bars show the selected value in the scene.

Revenue is based on item prices and excludes freight. The Olist tables do not contain profit, so the dashboard does not report profit.

## Data

This project uses the [Brazilian E-Commerce Public Dataset by Olist on Kaggle](https://www.kaggle.com/datasets/olistbr/brazilian-ecommerce). The downloaded compressed source tables are in `Datasets/OlistRaw/`; the Unity scene loads the joined order-line data from `Assets/Resources/OlistSales.csv`.

To rebuild the joined CSV, run:

```powershell
python Datasets/prepare_olist.py
```

The source dataset is licensed CC BY-NC-SA 4.0. See [Datasets/OlistSource.md](Datasets/OlistSource.md) for attribution and transformation notes.

## Preview

![Olist sales dashboard](Screenshots/olist-sales-dashboard.png)
