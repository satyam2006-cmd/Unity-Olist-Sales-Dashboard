# Salescope — 3D Olist Sales Dashboard

Salescope is an interactive 3D sales dashboard built in Unity. Its KPI values and chart geometry are calculated in C# from an editable CSV file. The scene is saved in Edit Mode with a clear hierarchy so the 3D objects are available for inspection and adjustment.

![Salescope green 3D sales dashboard](Screenshots/salescope-dashboard.png)

## Project readiness

**Ready for review and continued development in the Unity Editor.** The saved scene, live CSV refresh, data-driven charts, KPI calculations, click inspection, and green value palette are implemented. A temporary appended row was used to verify that KPI totals, bar widths, city column heights, and axis ranges respond to changed data; the original dataset was restored afterward.

This is an Editor-ready prototype. It is not currently packaged as a standalone desktop build or connected to a commerce database or live sales API. New data comes from edits to the local CSV described below.

## What is in the dashboard

- **KPI values:** unique customers, distinct orders, item revenue, average item revenue per order, delivered orders, units sold, and units per order.
- **Charts:** monthly revenue line and area chart, units by category donut, revenue by product horizontal bars, and revenue by city columns.
- **Live recalculation:** Unity C# reads the CSV and checks for saved file changes every 1.5 seconds while the scene is in Play Mode. It recalculates the KPI values and rebuilds the chart geometry and numeric axes from the new data.
- **Value styling:** charts share a green palette. Higher values use darker shades; lower values fade to pale green.
- **Interaction:** click KPI and chart marks in Play Mode to show the selected value in the dashboard status area.
- **Editable scene:** the root is `Salescope`; chart objects are grouped under `Retail Sales · 3D Dashboard` into KPI, line, donut, bar, column, and frame groups. Edit Mode shows placeholders for data-driven numbers and axes.

## Open and update the dashboard

1. Open this repository folder in **Unity 6.6.7f1**.
2. Open `Assets/Scenes/SalescopeDashboard.unity`.
3. Press **Play** to load the current values from `LiveData/OlistSales.csv`.
4. To add data, open `LiveData/OlistSales.csv` in Excel, append rows using the existing columns, and save as **CSV UTF-8**. While Play Mode is running, the dashboard refreshes after the save.

The expected columns are:

```text
Order ID, Order Date, Ship Date, Customer ID, Region, Category, Sales, Quantity, City, Sub-Category, Order Status
```

Use ISO dates such as `2026-11-14`, decimal points for numeric sales, and a new Order ID for each new order. Reusing an Order ID adds its line-item revenue and units but does not increase the distinct-order KPI. Customer count is also distinct by Customer ID.

The Edit Mode scene shows placeholders by design. Press Play to populate the saved chart hierarchy with values from the CSV. Use **Tools → Salescope → Build Editable 3D Dashboard** to regenerate the scene from the current CSV; rebuilding also replaces the existing scene objects.

## Dataset and calculations

The dashboard uses the [Brazilian E-Commerce Public Dataset by Olist on Kaggle](https://www.kaggle.com/datasets/olistbr/brazilian-ecommerce), covering Brazilian marketplace orders from 2016–2018. The source tables are included under `Datasets/OlistRaw/`, and the Excel-editable joined order-line file is `LiveData/OlistSales.csv`.

The current data produces approximately:

<div align="center">
  <table>
    <thead>
      <tr><th align="center">KPI</th><th align="center">Current value</th></tr>
    </thead>
    <tbody>
      <tr><td align="center">Customers</td><td align="center">96,096</td></tr>
      <tr><td align="center">Orders</td><td align="center">99,441</td></tr>
      <tr><td align="center">Item revenue</td><td align="center">R$13.59M</td></tr>
      <tr><td align="center">Average item revenue per order</td><td align="center">R$137</td></tr>
      <tr><td align="center">Delivered orders</td><td align="center">96,478</td></tr>
      <tr><td align="center">Units sold</td><td align="center">112,650</td></tr>
      <tr><td align="center">Units per order</td><td align="center">1.13</td></tr>
    </tbody>
  </table>
</div>

Revenue is the sum of item prices and excludes freight. The Olist tables do not contain profit, so the dashboard does not report profit. Monthly revenue is grouped chronologically and the chart displays the latest 12 months available in the CSV. Revenue axes are recalculated from the current values.

To regenerate the joined CSV from the original compressed source tables, run:

```powershell
python Datasets/prepare_olist.py
```

**This overwrites `LiveData/OlistSales.csv`.** Keep a backup first if you want to retain Excel edits or appended rows.

The dataset source is licensed **CC BY-NC-SA 4.0**. See [Datasets/OlistSource.md](Datasets/OlistSource.md) for attribution and transformation details.

## Repository layout

- `Assets/Scenes/SalescopeDashboard.unity` — saved editable Unity scene.
- `Assets/ProjectPulseDashboard.cs` — C# data loading, KPI calculations, chart generation, live file polling, and interactions.
- `LiveData/OlistSales.csv` — editable dataset used while the dashboard runs.
- `Datasets/OlistRaw/` — compressed Olist source tables.
- `Datasets/prepare_olist.py` — script to rebuild the joined CSV.
- `Screenshots/salescope-dashboard.png` — current dashboard preview.
