using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Persistent, editable Olist sales analytics dashboard built from 3D scene objects.</summary>
public sealed class ProjectPulseDashboard : MonoBehaviour
{
    static readonly Color Ink = Hex("#16352B");
    static readonly Color Muted = Hex("#43564D");
    static readonly Color Green = Hex("#19734F");
    static readonly Color Bright = Hex("#55B88A");
    static readonly Color Pale = Hex("#EAF4EE");
    static readonly Color White = Hex("#FFFFFF");
    static readonly Color Canvas = Hex("#F6F8F6");
    static readonly Color Grid = Hex("#DCE5DF");
    static readonly Color Amber = Hex("#E5AD45");
    static readonly Color Blue = Hex("#1688EF");
    static readonly Color Navy = Hex("#192E9B");
    static readonly Color Rose = Hex("#D96F70");

    Transform board;
    Camera viewCamera;
    TextMesh insightText;
    readonly Dictionary<Collider, string> clickDetails = new Dictionary<Collider, string>();
    SalesSnapshot sales;

    static Color Hex(string html) { ColorUtility.TryParseHtmlString(html, out var c); return c; }

    public void BuildInEditor()
    {
        if (Application.isPlaying) return;
        ClearChildren();
        BuildEnvironment();
        BuildDashboard();
    }

    void Start()
    {
        viewCamera = GetComponentInChildren<Camera>(true);
        board = transform.Find("Retail Sales · 3D Dashboard");
        if (board == null)
        {
            BuildEnvironment();
            BuildDashboard();
        }
    }

    void Update()
    {
        if (Mouse.current == null || viewCamera == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
        var ray = viewCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out var hit, 100f) && clickDetails.TryGetValue(hit.collider, out var detail) && insightText != null)
            insightText.text = detail;
    }

    void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
        clickDetails.Clear(); board = null; viewCamera = null;
    }

    void BuildEnvironment()
    {
        var cameraObject = new GameObject("Dashboard Camera", typeof(Camera));
        cameraObject.transform.SetParent(transform, false);
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0, 0, -10.5f);
        cameraObject.transform.LookAt(Vector3.zero);
        viewCamera = cameraObject.GetComponent<Camera>();
        viewCamera.clearFlags = CameraClearFlags.SolidColor;
        viewCamera.backgroundColor = Canvas;
        viewCamera.orthographic = true;
        viewCamera.orthographicSize = 3.25f;
        viewCamera.nearClipPlane = .05f;

        RenderSettings.ambientLight = new Color(.42f, .47f, .44f);
        RenderSettings.fog = false;
        AddLight("Dashboard Key Light", new Color(.93f, .97f, .94f), .72f, new Vector3(25, -20, 0));
        AddLight("Dashboard Fill Light", new Color(.70f, .90f, .79f), .22f, new Vector3(-25, 150, 0));
    }

    void AddLight(string name, Color color, float intensity, Vector3 rotation)
    {
        var go = new GameObject(name, typeof(Light)); go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(0, 0, -18); go.transform.rotation = Quaternion.Euler(rotation);
        var light = go.GetComponent<Light>(); light.type = LightType.Directional; light.color = color;
        light.intensity = intensity; light.shadows = LightShadows.None;
    }

    void BuildDashboard()
    {
        sales = LoadSalesSnapshot();
        board = new GameObject("Retail Sales · 3D Dashboard").transform;
        board.SetParent(transform, false);
        board.position = new Vector3(0, 0, -2.1f);
        board.rotation = Quaternion.Euler(-1.4f, 180f, 0);

        Panel("Dashboard Backing · 3D", Vector3.zero, 9.25f, 5.15f, .19f, Hex("#F1F5F2"), .16f);
        Box("Purple Navigation Rail · 3D", new Vector3(-4.53f, 0, .17f), .17f, 5.12f, .12f, Green, false);
        Box("Magenta Header Rule · 3D", new Vector3(-4.41f, 0, .17f), .012f, 5.12f, .03f, Bright, false);

        Text("SALESCOPE", new Vector3(-4.18f, 2.25f, .145f), .16f, Bright, TextAnchor.MiddleLeft, FontStyle.Bold);
        Text("BRAZILIAN E-COMMERCE  ·  OLIST  ·  2016–2018", new Vector3(-4.17f, 2.02f, .145f), .055f, Muted, TextAnchor.MiddleLeft);
        Panel("Active Page Selector · 3D", new Vector3(3.65f, 2.21f, .16f), 1.35f, .43f, .10f, Green, .12f, true, "Sales overview · Olist Brazilian e-commerce dataset, 2016–2018.");
        Sphere("Active Page Marker", new Vector3(3.18f, 2.21f, .24f), .085f, Bright);
        Text("OVERVIEW", new Vector3(3.36f, 2.21f, .245f), .054f, White, TextAnchor.MiddleLeft, FontStyle.Bold);

        BuildSalesMetrics();

        BuildWeeklyChart();
        BuildReminders();
        BuildProjectList();
        BuildTeamPanel();
        BuildProgressPanel();
        BuildInsightsPanel();
        OrganizeSceneHierarchy();
    }

    void OrganizeSceneHierarchy()
    {
        var support = transform.Find("Scene Support");
        if (support == null)
        {
            var supportObject = new GameObject("Scene Support");
            support = supportObject.transform;
            support.SetParent(transform, false);
        }
        foreach (Transform child in transform.Cast<Transform>().ToArray())
            if (child.name == "Dashboard Camera" || child.name == "Dashboard Key Light" || child.name == "Dashboard Fill Light")
                child.SetParent(support, true);

        string[] names = {
            "01 · KPI Cards",
            "02 · Line Chart · Revenue per Month",
            "03 · Donut Chart · Units by Category",
            "04 · Bar Chart · Revenue by Product",
            "05 · Column Chart · Revenue by City",
            "06 · Dashboard Frame & Insights"
        };
        var groups = new Dictionary<string, Transform>();
        foreach (string name in names)
        {
            var group = board.Find(name);
            if (group == null)
            {
                var groupObject = new GameObject(name);
                group = groupObject.transform;
                group.SetParent(board, false);
            }
            groups[name] = group;
        }

        var existingObjects = board.Cast<Transform>().Where(child => !groups.Values.Contains(child)).ToArray();
        foreach (Transform item in existingObjects)
        {
            string name = item.name;
            Vector3 position = item.position;
            string target;
            if (name.StartsWith("KPI ·") || name.StartsWith("Metric Divider") || (position.y > 1.25f && position.y < 1.90f))
                target = names[0];
            else if (name == "Dashboard Backing · 3D" || name.Contains("Navigation Rail") || name.Contains("Header Rule") ||
                     name.Contains("SALESCOPE") || name.Contains("BRAZILIAN E-COMMERCE") || position.y < -2.40f)
                target = names[5];
            else if (name.Contains("Monthly Revenue") || name.Contains("Revenue per Month") ||
                     name.Contains("Revenue Gridline ·") || name.Contains("Revenue Axis ·"))
                target = names[1];
            else if (name.Contains("Donut") || name.Contains("Units per Category") || name.Contains("Progress Legend Dot"))
                target = names[2];
            else if (name.Contains("Product Revenue") || name.Contains("Revenue by Product"))
                target = names[3];
            else if (name.Contains("City Revenue") || name.Contains("Revenue by City") || name.Contains("3D City Revenue Bar"))
                target = names[4];
            else if (name.Contains("Chart Frame"))
                target = position.y > -.85f ? (position.x > 1.55f ? names[2] : names[1]) : (position.x > 1f ? names[4] : names[3]);
            else if (position.y >= -.90f && position.y < 1.25f)
                target = position.x > 1.55f ? names[2] : names[1];
            else if (position.y < -.90f && position.y >= -2.40f)
                target = position.x > 1f ? names[4] : names[3];
            else
                target = names[5];

            item.SetParent(groups[target], true);
        }
    }

    void BuildSalesMetrics()
    {
        string[] labels = { "CUSTOMERS", "ORDERS", "ITEM REVENUE", "AVG ORDER", "DELIVERED", "ITEMS SOLD", "ITEMS / ORDER" };
        string[] values = { sales.CustomerCount.ToString("N0"), sales.OrderCount.ToString("N0"), Money(sales.TotalSales), Money(sales.OrderCount == 0 ? 0 : sales.TotalSales / sales.OrderCount), sales.DeliveredOrders.ToString("N0"), sales.TotalUnits.ToString("N0"), sales.AverageUnitsPerOrder.ToString("0.00") };
        string[] details = {
            sales.CustomerCount.ToString("N0") + " unique customers in this dataset.",
            sales.OrderCount.ToString("N0") + " distinct order IDs.",
            "Revenue total: " + Money(sales.TotalSales) + ".",
            "Item revenue: " + Money(sales.TotalSales) + "; freight is excluded.",
            "Average item revenue per order: " + Money(sales.OrderCount == 0 ? 0 : sales.TotalSales / sales.OrderCount) + ".",
            sales.DeliveredOrders.ToString("N0") + " orders have delivered status in the source dataset.",
            sales.TotalUnits.ToString("N0") + " order-item rows; each row is one item.",
            sales.AverageUnitsPerOrder.ToString("0.00") + " order items per order on average."
        };
        for (int i = 0; i < labels.Length; i++)
        {
            float x = -3.91f + i * 1.30f;
            Color color = (i == 0 || i == 2 || i == 5) ? Bright : Ink;
            Text(values[i], new Vector3(x, 1.63f, .20f), .112f, color, TextAnchor.MiddleCenter, FontStyle.Bold);
            Text(labels[i], new Vector3(x, 1.39f, .20f), .043f, Green, TextAnchor.MiddleCenter);
            var hit = BoxClickable("KPI · " + labels[i], new Vector3(x, 1.56f, .235f), 1.18f, .52f, .04f, White, details[i]);
            hit.GetComponent<Renderer>().enabled = false;
        }
        Box("Metric Divider · 3D", new Vector3(0, 1.22f, .19f), 8.10f, .012f, .025f, Grid, false);
    }

    void BuildKpi(string title, string value, string caption, float x, Color fill, Color valueColor, string insight)
    {
        var tile = Panel("KPI Card · " + title, new Vector3(x, 1.46f, .13f), 2.18f, .91f, .13f, fill, .13f, true, insight);
        Color labelColor = fill == Green ? new Color(.87f, .96f, .90f) : Muted;
        Text(title, new Vector3(x - .91f, 1.72f, .235f), .064f, labelColor, TextAnchor.MiddleLeft, FontStyle.Bold);
        Text(value, new Vector3(x - .91f, 1.40f, .24f), .24f, valueColor, TextAnchor.MiddleLeft, FontStyle.Bold);
        Text(caption, new Vector3(x - .91f, 1.13f, .24f), .053f, labelColor, TextAnchor.MiddleLeft);
        if (fill == Green) Box("KPI Accent", new Vector3(x - 1.00f, 1.46f, .235f), .055f, .57f, .035f, Bright, false);
    }

    void BuildWeeklyChart()
    {
        Panel("Chart · Revenue per Month · 3D", new Vector3(-1.34f, .05f, .12f), 5.90f, 1.91f, .12f, White, .10f);
        Text("REVENUE PER MONTH", new Vector3(-1.34f, .79f, .225f), .075f, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
        float x0 = -4.02f, x1 = 1.32f, baseY = -.57f, topY = .55f, max = Mathf.Ceil((float)sales.MaxMonthlySales / 100000f) * 100000f;
        for (int tick = 0; tick <= 2; tick++)
        {
            float y = Mathf.Lerp(baseY, topY, tick / 2f);
            float value = max * tick / 2f;
            Box("Revenue Gridline · " + Money(value), new Vector3((x0 + x1) * .5f, y, .225f), x1 - x0, .012f, .025f, Grid, false);
            Text(Money(value), new Vector3(x0 - .04f, y, .245f), .041f, Muted, TextAnchor.MiddleRight);
        }
        Box("Revenue Axis · X", new Vector3((x0 + x1) * .5f, baseY, .245f), x1 - x0, .022f, .045f, Muted, false);
        Box("Revenue Axis · Y", new Vector3(x0, (baseY + topY) * .5f, .245f), .022f, topY - baseY, .045f, Muted, false);
        string[] months = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
        var points = new List<Vector3>();
        float step = (x1 - x0) / 11f;
        for (int i = 0; i < 12; i++)
        {
            float x = x0 + i * step;
            float y = baseY + (topY - baseY) * (float)(sales.MonthlySales[i] / max);
            points.Add(new Vector3(x, y, .29f));
            Text(months[i], new Vector3(x, baseY - .16f, .26f), .046f, Muted, TextAnchor.MiddleCenter);
        }
        List<Vector3> smooth = SmoothPoints(points, 3);
        AreaMesh("Monthly Revenue Area · 3D Mesh", smooth, baseY, .255f, Hex("#D7EEDF"));
        for (int i = 0; i < smooth.Count - 1; i++) Segment("Monthly Revenue Curve · 3D", smooth[i], smooth[i + 1], .026f, Green);
        for (int i = 0; i < points.Count; i++) SphereClickable("Monthly Revenue Mark · " + months[i], points[i], .042f, Bright,
            months[i] + " item revenue: " + Money(sales.MonthlySales[i]) + " across 2016–2018.");
        ChartFrame(-1.34f, .05f, 5.90f, 1.91f);
    }

    void BuildReminders()
    {
        Panel("Chart · Units per Category · 3D", new Vector3(3.12f, .05f, .12f), 2.88f, 1.91f, .12f, White, .10f);
        Text("UNITS PER CATEGORY", new Vector3(3.12f, .79f, .225f), .073f, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
        var leading = sales.CategoryUnits.Where(x => x.Value > 0).OrderByDescending(x => x.Value).Take(3).ToArray();
        var categories = leading.Select(x => x.Key).ToList();
        int otherUnits = sales.TotalUnits - leading.Sum(x => x.Value);
        if (otherUnits > 0) categories.Add("Other categories");
        Color[] colors = { Green, Bright, Amber, Blue };
        int total = Math.Max(1, sales.TotalUnits);
        float angle = -90f;
        for (int i = 0; i < categories.Count; i++)
        {
            int units = categories[i] == "Other categories" ? otherUnits : sales.CategoryUnits[categories[i]];
            float share = (float)units / total;
            float end = angle + share * 360f;
            Donut(new Vector3(2.46f, .01f, .36f), .60f, .37f, .16f, angle, end, colors[i], categories[i]);
            angle = end + 1.4f;
        }
        Text(CountShort(sales.TotalUnits), new Vector3(2.46f, .06f, .47f), .085f, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
        Text("UNITS", new Vector3(2.46f, -.11f, .47f), .039f, Muted, TextAnchor.MiddleCenter);
        for (int i = 0; i < categories.Count; i++)
        {
            int units = categories[i] == "Other categories" ? otherUnits : sales.CategoryUnits[categories[i]];
            LegendDot(3.30f, .40f - i * .24f, colors[i], ShortCategory(categories[i]) + " · " + CountShort(units));
        }
        ChartFrame(3.12f, .05f, 2.88f, 1.91f);
    }

    void BuildProjectList()
    {
        Panel("Chart · Revenue by Product · 3D", new Vector3(-1.72f, -1.70f, .12f), 5.38f, 1.40f, .12f, White, .10f);
        Text("TOTAL REVENUE", new Vector3(-4.23f, -1.08f, .225f), .071f, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
        Text("by product category", new Vector3(-4.23f, -1.20f, .225f), .046f, Muted, TextAnchor.MiddleLeft);
        var products = sales.SubcategorySales.OrderByDescending(x => x.Value).Take(8).ToArray();
        float max = (float)products[0].Value;
        Color[] palette = { Green, Hex("#277C5A"), Amber, Bright, Hex("#3D956D"), Hex("#91CDAA"), Blue, Hex("#527F65"), Hex("#D9B800") };
        for (int i = 0; i < products.Length; i++)
        {
            float y = -1.35f - i * .133f;
            float ratio = (float)products[i].Value / max;
            Text(products[i].Key, new Vector3(-4.23f, y, .27f), .043f, Muted, TextAnchor.MiddleLeft);
            Box("Product Revenue Bar · " + products[i].Key, new Vector3(-2.84f + 1.375f * ratio, y, .30f), 2.75f * ratio, .072f, .065f, palette[i], true,
                products[i].Key + " sales: " + Money(products[i].Value) + ".");
            Text(Money(products[i].Value), new Vector3(.52f, y, .27f), .040f, Muted, TextAnchor.MiddleRight);
        }
        ChartFrame(-1.72f, -1.70f, 5.38f, 1.40f);
    }

    void BuildTeamPanel()
    {
        Panel("Chart · Revenue by City · 3D", new Vector3(2.83f, -1.70f, .12f), 3.52f, 1.40f, .12f, White, .10f);
        Text("TOTAL REVENUE", new Vector3(1.20f, -1.08f, .225f), .071f, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
        Text("by city · top 7", new Vector3(1.20f, -1.20f, .225f), .046f, Muted, TextAnchor.MiddleLeft);
        var cities = sales.CitySales.OrderByDescending(x => x.Value).Take(7).ToArray();
        float x0 = 1.38f, x1 = 4.28f, baseY = -2.15f, topY = -1.38f;
        float max = Mathf.Ceil((float)cities[0].Value / 100000f) * 100000f;
        if (max <= 0) max = 100000f;
        for (int tick = 0; tick <= 2; tick++)
        {
            float y = Mathf.Lerp(baseY, topY, tick / 2f);
            float value = max * tick / 2f;
            Box("City Revenue Gridline", new Vector3((x0 + x1) * .5f, y, .235f), x1 - x0, .012f, .025f, Grid, false);
            Text(Money(value), new Vector3(x0 - .05f, y, .25f), .036f, Muted, TextAnchor.MiddleRight);
        }
        Box("City Revenue X Axis", new Vector3((x0 + x1) * .5f, baseY, .25f), x1 - x0, .022f, .045f, Muted, false);
        Box("City Revenue Y Axis", new Vector3(x0, (baseY + topY) * .5f, .25f), .022f, topY - baseY, .045f, Muted, false);
        float step = (x1 - x0) / cities.Length;
        for (int i = 0; i < cities.Length; i++)
        {
            float x = x0 + step * (i + .5f);
            float h = (topY - baseY) * Mathf.Clamp01((float)cities[i].Value / max);
            Color barColor = i == 0 ? Green : (i < 3 ? Bright : Hex("#8DC9A7"));
            Box("3D City Revenue Bar · " + cities[i].Key, new Vector3(x, baseY + h * .5f, .32f), step * .58f, h, .18f, barColor, true,
                cities[i].Key + " sales: " + Money(cities[i].Value) + ".");
            Text(ShortCity(cities[i].Key), new Vector3(x, baseY - .12f, .26f), .039f, Muted, TextAnchor.MiddleCenter);
        }
        ChartFrame(2.83f, -1.70f, 3.52f, 1.40f);
    }

    void BuildProgressPanel()
    {
        // The reference composition uses four analytical panels; no extra filler card here.
    }

    void BuildInsightsPanel()
    {
        insightText = Text("CLICK A KPI OR CHART MARK TO INSPECT THE VALUE", new Vector3(0, -2.49f, .18f), .038f, Muted, TextAnchor.MiddleCenter);
    }

    sealed class SalesSnapshot
    {
        public double TotalSales, TotalProfit;
        public int OrderCount, DeliveredOrders, CustomerCount, TotalUnits, PeakMonthIndex;
        public float AverageUnitsPerOrder;
        public readonly double[] MonthlySales = new double[12];
        public double MaxMonthlySales;
        public readonly Dictionary<string, double> CategorySales = new Dictionary<string, double>();
        public readonly Dictionary<string, double> CategoryProfit = new Dictionary<string, double>();
        public readonly Dictionary<string, int> CategoryUnits = new Dictionary<string, int>();
        public readonly Dictionary<string, double> RegionSales = new Dictionary<string, double>();
        public readonly Dictionary<string, double> SubcategorySales = new Dictionary<string, double>();
        public readonly Dictionary<string, double> CitySales = new Dictionary<string, double>();
    }

    SalesSnapshot LoadSalesSnapshot()
    {
        var result = new SalesSnapshot();
        TextAsset csv = Resources.Load<TextAsset>("OlistSales");
        if (csv == null) { Debug.LogError("Missing Resources/OlistSales.csv Olist dataset."); return result; }
        string[] lines = csv.text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) return result;
        var headers = CsvLine(lines[0]);
        int orderCol = headers.IndexOf("Order ID"), dateCol = headers.IndexOf("Order Date");
        int regionCol = headers.IndexOf("Region"), categoryCol = headers.IndexOf("Category");
        int salesCol = headers.IndexOf("Sales");
        int customerCol = headers.IndexOf("Customer ID"), unitsCol = headers.IndexOf("Quantity");
        int shipCol = headers.IndexOf("Ship Date"), cityCol = headers.IndexOf("City"), subcategoryCol = headers.IndexOf("Sub-Category");
        int statusCol = headers.IndexOf("Order Status");
        var orders = new HashSet<string>(); var delivered = new HashSet<string>(); var customers = new HashSet<string>();
        CultureInfo dateCulture = CultureInfo.InvariantCulture;
        for (int row = 1; row < lines.Length; row++)
        {
            List<string> fields = CsvLine(lines[row]);
            int required = new[] { orderCol, dateCol, regionCol, categoryCol, salesCol, customerCol, unitsCol, shipCol, cityCol, subcategoryCol, statusCol }.Max();
            if (fields.Count <= required) continue;
            if (!double.TryParse(fields[salesCol], NumberStyles.Float, CultureInfo.InvariantCulture, out double amount)) continue;
            result.TotalSales += amount; orders.Add(fields[orderCol]); customers.Add(fields[customerCol]);
            if (fields[statusCol] == "delivered") delivered.Add(fields[orderCol]);
            int quantity = 0; int.TryParse(fields[unitsCol], NumberStyles.Integer, CultureInfo.InvariantCulture, out quantity); result.TotalUnits += quantity;
            string category = fields[categoryCol], region = fields[regionCol];
            if (!result.CategorySales.ContainsKey(category))
            { result.CategorySales[category] = 0; result.CategoryProfit[category] = 0; result.CategoryUnits[category] = 0; }
            result.CategorySales[category] += amount; result.CategoryUnits[category] += quantity;
            if (!result.RegionSales.ContainsKey(region)) result.RegionSales[region] = 0;
            if (result.RegionSales.ContainsKey(region)) result.RegionSales[region] += amount;
            string subcategory = fields[subcategoryCol], city = fields[cityCol];
            if (!result.SubcategorySales.ContainsKey(subcategory)) result.SubcategorySales[subcategory] = 0;
            result.SubcategorySales[subcategory] += amount;
            if (!result.CitySales.ContainsKey(city)) result.CitySales[city] = 0;
            result.CitySales[city] += amount;
            if (DateTime.TryParse(fields[dateCol], dateCulture, DateTimeStyles.None, out DateTime date))
                result.MonthlySales[date.Month - 1] += amount;
        }
        result.OrderCount = orders.Count; result.DeliveredOrders = delivered.Count; result.CustomerCount = customers.Count;
        result.AverageUnitsPerOrder = result.OrderCount == 0 ? 0 : (float)result.TotalUnits / result.OrderCount;
        for (int i = 0; i < result.MonthlySales.Length; i++)
            if (result.MonthlySales[i] > result.MaxMonthlySales)
            { result.MaxMonthlySales = result.MonthlySales[i]; result.PeakMonthIndex = i; }
        return result;
    }

    static List<string> CsvLine(string line)
    {
        var fields = new List<string>(); var field = new System.Text.StringBuilder(); bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"') { field.Append('"'); i++; }
                else quoted = !quoted;
            }
            else if (c == ',' && !quoted) { fields.Add(field.ToString().Trim()); field.Length = 0; }
            else field.Append(c);
        }
        fields.Add(field.ToString().Trim()); return fields;
    }

    static string Money(double value)
    {
        double absolute = Math.Abs(value);
        if (absolute >= 1000000) return "R$" + (value / 1000000d).ToString("0.00", CultureInfo.InvariantCulture) + "M";
        if (absolute >= 1000) return "R$" + (value / 1000d).ToString("0", CultureInfo.InvariantCulture) + "K";
        return "R$" + value.ToString("0", CultureInfo.InvariantCulture);
    }

    static string CountShort(int value) => value >= 1000 ? (value / 1000f).ToString("0.#", CultureInfo.InvariantCulture) + "K" : value.ToString("N0");
    static string ShortCategory(string value) => value.Length <= 17 ? value : value.Substring(0, 16) + "…";
    static string ShortCity(string value)
    {
        string[] parts = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1) return parts[0].Length <= 5 ? parts[0] : parts[0].Substring(0, 5);
        string first = parts[0].Length <= 3 ? parts[0] : parts[0].Substring(0, 3);
        string second = parts[parts.Length - 1].Length == 0 ? "" : parts[parts.Length - 1].Substring(0, 1);
        return first + "." + second;
    }

    GameObject BoxClickable(string name, Vector3 pos, float w, float h, float d, Color color, string detail)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(board, false);
        go.transform.localPosition = LayoutPosition(pos); go.transform.localScale = new Vector3(w, h, d);
        go.GetComponent<Renderer>().sharedMaterial = Material(color);
        clickDetails[go.GetComponent<Collider>()] = detail;
        return go;
    }

    void SphereClickable(string name, Vector3 pos, float radius, Color color, string detail)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = name; go.transform.SetParent(board, false);
        go.transform.localPosition = LayoutPosition(pos); go.transform.localScale = Vector3.one * radius * 2f;
        go.GetComponent<Renderer>().sharedMaterial = Material(color);
        clickDetails[go.GetComponent<Collider>()] = detail;
    }

    void ChartFrame(float x, float y, float w, float h)
    {
        float z = .205f, thickness = .012f;
        Box("Chart Frame · Top", new Vector3(x, y + h * .5f, z), w, thickness, .028f, Hex("#8C8790"), false);
        Box("Chart Frame · Bottom", new Vector3(x, y - h * .5f, z), w, thickness, .028f, Hex("#8C8790"), false);
        Box("Chart Frame · Left", new Vector3(x - w * .5f, y, z), thickness, h, .028f, Hex("#8C8790"), false);
        Box("Chart Frame · Right", new Vector3(x + w * .5f, y, z), thickness, h, .028f, Hex("#8C8790"), false);
    }

    static List<Vector3> SmoothPoints(List<Vector3> points, int subdivisions)
    {
        var smooth = new List<Vector3>();
        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 p0 = points[Mathf.Max(i - 1, 0)], p1 = points[i], p2 = points[i + 1], p3 = points[Mathf.Min(i + 2, points.Count - 1)];
            for (int step = 0; step < subdivisions; step++)
            {
                float t = step / (float)subdivisions, t2 = t * t, t3 = t2 * t;
                Vector3 p = .5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
                smooth.Add(p);
            }
        }
        smooth.Add(points[points.Count - 1]); return smooth;
    }

    void AreaMesh(string name, List<Vector3> points, float baseY, float z, Color color)
    {
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        for (int i = 0; i < points.Count - 1; i++)
        {
            int n = vertices.Count;
            vertices.Add(LayoutPosition(new Vector3(points[i].x, points[i].y, z)));
            vertices.Add(LayoutPosition(new Vector3(points[i + 1].x, points[i + 1].y, z)));
            vertices.Add(LayoutPosition(new Vector3(points[i + 1].x, baseY, z)));
            vertices.Add(LayoutPosition(new Vector3(points[i].x, baseY, z)));
            triangles.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(board, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = Material(color);
    }

    void Segment(string name, Vector3 a, Vector3 b, float thickness, Color color)
    {
        Vector3 localA = LayoutPosition(a), localB = LayoutPosition(b), delta = localB - localA;
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(board, false);
        go.transform.localPosition = (localA + localB) * .5f;
        go.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        go.transform.localScale = new Vector3(delta.magnitude, thickness, .045f);
        go.GetComponent<Renderer>().sharedMaterial = Material(color); DestroyImmediate(go.GetComponent<Collider>());
    }

    void LegendDot(float x, float y, Color color, string label)
    {
        Sphere("Progress Legend Dot · " + label, new Vector3(x, y, .30f), .045f, color);
        Text(label, new Vector3(x + .12f, y, .30f), .044f, Muted, TextAnchor.MiddleLeft);
    }

    GameObject Panel(string name, Vector3 pos, float w, float h, float d, Color color, float radius, bool clickable = false, string detail = null)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(board, false); go.transform.localPosition = LayoutPosition(pos);
        go.GetComponent<MeshFilter>().sharedMesh = RoundedMesh(w, h, d, radius);
        go.GetComponent<MeshRenderer>().sharedMaterial = Material(color);
        if (clickable)
        {
            var collider = go.AddComponent<BoxCollider>(); collider.size = new Vector3(w, h, d * 5f);
            if (detail != null) clickDetails[collider] = detail;
        }
        return go;
    }

    void Box(string name, Vector3 pos, float w, float h, float d, Color color, bool clickable, string detail = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(board, false);
        go.transform.localPosition = LayoutPosition(pos); go.transform.localScale = new Vector3(w, h, d);
        go.GetComponent<Renderer>().sharedMaterial = Material(color);
        if (!clickable) DestroyImmediate(go.GetComponent<Collider>());
        else if (detail != null) clickDetails[go.GetComponent<Collider>()] = detail;
    }

    void Sphere(string name, Vector3 pos, float radius, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = name; go.transform.SetParent(board, false);
        go.transform.localPosition = LayoutPosition(pos); go.transform.localScale = Vector3.one * radius * 2f;
        go.GetComponent<Renderer>().sharedMaterial = Material(color); DestroyImmediate(go.GetComponent<Collider>());
    }

    TextMesh Text(string value, Vector3 pos, float size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
    {
        var go = new GameObject("3D Text · " + value); go.transform.SetParent(board, false);
        go.transform.localPosition = LayoutPosition(pos); go.transform.localRotation = Quaternion.identity;
        var mesh = go.AddComponent<TextMesh>(); mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        mesh.fontSize = 100; mesh.characterSize = size * .14f; mesh.anchor = anchor;
        go.transform.localScale = new Vector3(-1f, 1f, 1f);
        mesh.alignment = TextAlignment.Left; mesh.fontStyle = style; mesh.color = color; mesh.text = value; mesh.richText = false;
        return mesh;
    }

    void Donut(Vector3 center, float outer, float inner, float depth, float start, float end, Color color, string detail)
    {
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        const int steps = 32; float half = depth / 2f;
        for (int i = 0; i <= steps; i++)
        {
            float a = Mathf.Lerp(start, end, i / (float)steps) * Mathf.Deg2Rad;
            float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            vertices.Add(new Vector3(cs * outer, sn * outer, half)); vertices.Add(new Vector3(cs * inner, sn * inner, half));
            vertices.Add(new Vector3(cs * outer, sn * outer, -half)); vertices.Add(new Vector3(cs * inner, sn * inner, -half));
        }
        for (int i = 0; i < steps; i++)
        {
            int a = i * 4, b = a + 4;
            triangles.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1, a + 2, a + 3, b + 2, a + 3, b + 3, b + 2,
                a, a + 2, b, b, a + 2, b + 2, a + 1, b + 1, a + 3, b + 1, b + 3, a + 3 });
        }
        var mesh = new Mesh { name = "3D Portfolio Progress Arc" }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var go = new GameObject("3D Donut Segment · " + detail, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(board, false); go.transform.localPosition = LayoutPosition(center);
        go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = Material(color);
    }

    Mesh RoundedMesh(float w, float h, float d, float r)
    {
        const int n = 6; var ring = new List<Vector2>();
        Vector2[] centers = { new Vector2(w/2-r,h/2-r), new Vector2(-w/2+r,h/2-r), new Vector2(-w/2+r,-h/2+r), new Vector2(w/2-r,-h/2+r) };
        for (int c=0;c<4;c++) for(int i=0;i<=n;i++)
        { float a=(c*90f+i*90f/n)*Mathf.Deg2Rad; ring.Add(centers[c]+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r); }
        int count=ring.Count, back=count+1, backRing=back+1;
        var vertices=new List<Vector3>{Vector3.zero};
        for(int i=0;i<count;i++) vertices.Add(new Vector3(ring[i].x,ring[i].y,d/2));
        vertices.Add(new Vector3(0,0,-d/2));
        for(int i=0;i<count;i++) vertices.Add(new Vector3(ring[i].x,ring[i].y,-d/2));
        var tris=new List<int>();
        for(int i=0;i<count;i++) { int j=(i+1)%count; tris.AddRange(new[]{0,i+1,j+1,back,backRing+j,backRing+i,i+1,backRing+i,j+1,j+1,backRing+i,backRing+j}); }
        var mesh=new Mesh{name="Rounded 3D Dashboard Panel"}; mesh.SetVertices(vertices); mesh.SetTriangles(tris,0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }

    static Vector3 LayoutPosition(Vector3 p) => new Vector3(-p.x, p.y, p.z);
    static Material Material(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(shader) { color = color };
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .04f);
        return m;
    }
}
