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
    static readonly Color GreenMid = Hex("#348B67");
    static readonly Color GreenSoft = Hex("#74B795");
    static readonly Color GreenPale = Hex("#A7D6BA");

    Transform board;
    Camera viewCamera;
    TextMesh insightText;
    readonly Dictionary<Collider, string> clickDetails = new Dictionary<Collider, string>();
    readonly HashSet<Material> generatedMaterials = new HashSet<Material>();
    readonly HashSet<Mesh> generatedMeshes = new HashSet<Mesh>();
    SalesSnapshot sales;
    string liveDataPath;
    string lastLoadedDataStamp;
    int lastLoadedContentHash;
    float nextDataPollTime;
    string lastDataLoadError;
    const float DataPollIntervalSeconds = 1.5f;

    static Color Hex(string html) { ColorUtility.TryParseHtmlString(html, out var c); return c; }

    public void BuildInEditor()
    {
        if (Application.isPlaying) return;
        ClearChildren();
        BuildEnvironment();
        BuildDashboard();
        SetDataPlaceholdersInEditor();
        ApplyGreenThemeInEditor();
    }

    public void SetDataPlaceholdersInEditor()
    {
        if (Application.isPlaying) return;
        board = transform.Find("Retail Sales · 3D Dashboard");
        FindInsightText();
        SetDataPlaceholders();
    }

    public void ApplyGreenThemeInEditor()
    {
        if (Application.isPlaying) return;
        if (sales == null) sales = LoadSalesSnapshot();
        board = transform.Find("Retail Sales · 3D Dashboard");
        if (board == null) return;
        var shades = new[] { Green, GreenMid, GreenSoft, GreenPale };
        ApplyValuePalette(board.Find("04 · Bar Chart · Revenue by Product"), "Product Revenue Bar · ", shades,
            renderer => LookupValue(sales.SubcategorySales, renderer.name, "Product Revenue Bar · "));
        ApplyValuePalette(board.Find("05 · Column Chart · Revenue by City"), "3D City Revenue Bar · ", shades,
            renderer => LookupValue(sales.CitySales, renderer.name, "3D City Revenue Bar · "));

        var leadingCategories = sales.CategoryUnits.OrderByDescending(pair => pair.Value).Take(3).ToArray();
        int otherUnits = sales.TotalUnits - leadingCategories.Sum(pair => pair.Value);
        var donutValues = leadingCategories.ToDictionary(pair => pair.Key, pair => (double)pair.Value);
        if (otherUnits > 0) donutValues["Other categories"] = otherUnits;
        ApplyValuePalette(board.Find("03 · Donut Chart · Units by Category"), "3D Donut Segment · ", shades,
            renderer => LookupValue(donutValues, renderer.name, "3D Donut Segment · "));
    }

    static double LookupValue<T>(IDictionary<string, T> values, string objectName, string prefix) where T : IConvertible
    {
        string key = objectName.Substring(prefix.Length);
        return values.TryGetValue(key, out T value) ? Convert.ToDouble(value, CultureInfo.InvariantCulture) : 0;
    }

    static void ApplyValuePalette(Transform group, string objectPrefix, Color[] palette, Func<Renderer, double> valueSelector)
    {
        if (group == null) return;
        var renderers = group.GetComponentsInChildren<Renderer>(true)
            .Where(renderer => renderer.name.StartsWith(objectPrefix, StringComparison.Ordinal))
            .OrderByDescending(valueSelector).ToArray();
        for (int i = 0; i < renderers.Length; i++)
        {
            var material = renderers[i].sharedMaterial;
            int shadeIndex = renderers.Length <= 1 ? 0 : Mathf.RoundToInt(i * (palette.Length - 1f) / (renderers.Length - 1f));
            if (material != null) material.color = palette[shadeIndex];
        }
    }

    void Start()
    {
        viewCamera = GetComponentInChildren<Camera>(true);
        liveDataPath = GetLiveDataPath();
        string csv = ReadLiveData();
        sales = ParseSalesSnapshot(csv);
        lastLoadedContentHash = csv.GetHashCode();
        lastLoadedDataStamp = GetLiveDataStamp();
        board = transform.Find("Retail Sales · 3D Dashboard");
        if (board == null)
        {
            BuildEnvironment();
            BuildDashboard();
        }
        else
        {
            TextAsset baseline = Resources.Load<TextAsset>("OlistSales");
            if ((baseline != null && !string.Equals(csv, baseline.text, StringComparison.Ordinal)) || HasDataPlaceholders())
                RebuildDataGroups();
        }
        FindInsightText();
        SetLiveStatus("LIVE CSV · " + sales.RowCount.ToString("N0", CultureInfo.InvariantCulture) + " ROWS · APPEND ROWS IN EXCEL");
    }

    void Update()
    {
        PollLiveData();
        if (Mouse.current == null || viewCamera == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
        var ray = viewCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out var hit, 100f) && clickDetails.TryGetValue(hit.collider, out var detail) && insightText != null)
            insightText.text = detail;
    }

    void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            DestroyGeneratedResources(child);
            DestroyImmediate(child);
        }
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

    void RebuildDataGroups()
    {
        if (board == null || sales == null) return;
        clickDetails.Clear();
        RebuildGroup("01 · KPI Cards", BuildSalesMetrics);
        RebuildGroup("02 · Line Chart · Revenue per Month", BuildWeeklyChart);
        RebuildGroup("03 · Donut Chart · Units by Category", BuildReminders);
        RebuildGroup("04 · Bar Chart · Revenue by Product", BuildProjectList);
        RebuildGroup("05 · Column Chart · Revenue by City", BuildTeamPanel);
        FindInsightText();
    }

    void RebuildGroup(string groupName, Action build)
    {
        var group = board.Find(groupName);
        if (group == null)
        {
            var groupObject = new GameObject(groupName);
            group = groupObject.transform;
            group.SetParent(board, false);
        }
        for (int i = group.childCount - 1; i >= 0; i--)
        {
            var child = group.GetChild(i).gameObject;
            DestroyGeneratedResources(child);
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
        Transform previousBoard = board;
        board = group;
        try { build(); }
        finally { board = previousBoard; }
    }

    void DestroyGeneratedResources(GameObject owner)
    {
        foreach (Renderer renderer in owner.GetComponentsInChildren<Renderer>(true))
        {
            Material material = renderer.sharedMaterial;
            if (material == null || !generatedMaterials.Remove(material)) continue;
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
        }
        foreach (MeshFilter filter in owner.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null || !generatedMeshes.Remove(mesh)) continue;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }
    }

    void FindInsightText()
    {
        if (board == null) return;
        var group = board.Find("06 · Dashboard Frame & Insights");
        if (group == null) return;
        insightText = group.GetComponentsInChildren<TextMesh>(true).FirstOrDefault(text => text.name.Contains("LIVE DATA STATUS"));
    }

    bool HasDataPlaceholders()
    {
        if (board == null) return false;
        return board.GetComponentsInChildren<TextMesh>(true).Any(text => text.text == "—");
    }

    void SetDataPlaceholders()
    {
        if (board == null) return;
        var kpiGroup = board.Find("01 · KPI Cards");
        if (kpiGroup != null)
        {
            string[] labels = { "CUSTOMERS", "ORDERS", "ITEM REVENUE", "AVG ORDER", "DELIVERED", "ITEMS SOLD", "ITEMS / ORDER" };
            var values = kpiGroup.GetComponentsInChildren<TextMesh>(true)
                .Where(text => text.text.Any(char.IsDigit) && text.transform.position.y > 1.45f)
                .OrderBy(text => text.transform.position.x).ToArray();
            for (int i = 0; i < values.Length; i++)
            {
                values[i].text = "—";
                values[i].name = "KPI Value Placeholder · " + labels[Mathf.Min(i, labels.Length - 1)];
            }
        }

        string[] chartGroups = {
            "02 · Line Chart · Revenue per Month",
            "03 · Donut Chart · Units by Category",
            "04 · Bar Chart · Revenue by Product",
            "05 · Column Chart · Revenue by City"
        };
        foreach (string groupName in chartGroups)
        {
            var group = board.Find(groupName);
            if (group == null) continue;
            foreach (TextMesh text in group.GetComponentsInChildren<TextMesh>(true))
            {
                if (text.text.StartsWith("R$", StringComparison.Ordinal))
                {
                    text.text = "—";
                    text.name = "Chart Axis Placeholder · " + groupName;
                }
                else if (groupName.Contains("Donut") && text.text.Any(char.IsDigit))
                {
                    int separator = text.text.LastIndexOf('·');
                    text.text = separator >= 0 ? text.text.Substring(0, separator + 1) + " —" : "—";
                    text.name = "Chart Value Placeholder · Units";
                }
            }
        }
    }

    void SetLiveStatus(string value)
    {
        if (insightText != null) insightText.text = value;
    }

    string GetLiveDataPath()
    {
        string projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
        return System.IO.Path.Combine(projectRoot, "LiveData", "OlistSales.csv");
    }

    void EnsureLiveDataFile()
    {
        liveDataPath = GetLiveDataPath();
        if (System.IO.File.Exists(liveDataPath)) return;
        var seed = Resources.Load<TextAsset>("OlistSales");
        if (seed == null) throw new InvalidOperationException("Missing Olist sales CSV. Restore Assets/Resources/OlistSales.csv.");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(liveDataPath));
        System.IO.File.WriteAllText(liveDataPath, seed.text, new System.Text.UTF8Encoding(false));
    }

    string ReadLiveData()
    {
        EnsureLiveDataFile();
        return System.IO.File.ReadAllText(liveDataPath, System.Text.Encoding.UTF8);
    }

    string GetLiveDataStamp()
    {
        var info = new System.IO.FileInfo(liveDataPath);
        return info.Exists ? info.Length + ":" + info.LastWriteTimeUtc.Ticks : string.Empty;
    }

    void PollLiveData()
    {
        if (!Application.isPlaying || string.IsNullOrEmpty(liveDataPath) || Time.unscaledTime < nextDataPollTime) return;
        nextDataPollTime = Time.unscaledTime + DataPollIntervalSeconds;
        string stamp = GetLiveDataStamp();
        if (stamp == lastLoadedDataStamp) return;
        try
        {
            string csv = System.IO.File.ReadAllText(liveDataPath, System.Text.Encoding.UTF8);
            int contentHash = csv.GetHashCode();
            if (contentHash == lastLoadedContentHash)
            {
                lastLoadedDataStamp = stamp;
                return;
            }
            sales = ParseSalesSnapshot(csv);
            lastLoadedContentHash = contentHash;
            lastLoadedDataStamp = stamp;
            lastDataLoadError = null;
            RebuildDataGroups();
            SetLiveStatus("LIVE CSV UPDATED · " + sales.RowCount.ToString("N0", CultureInfo.InvariantCulture) + " ROWS · " + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
            Debug.Log("Salescope recalculated from " + liveDataPath + " (" + sales.RowCount.ToString("N0", CultureInfo.InvariantCulture) + " order lines).");
        }
        catch (Exception exception)
        {
            string message = "Live sales CSV is not ready yet: " + exception.Message;
            if (message != lastDataLoadError) Debug.LogWarning(message);
            lastDataLoadError = message;
            SetLiveStatus("LIVE CSV INVALID · CHECK HEADERS / SAVE AGAIN");
        }
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
            "Average item revenue per order: " + Money(sales.OrderCount == 0 ? 0 : sales.TotalSales / sales.OrderCount) + ".",
            sales.DeliveredOrders.ToString("N0") + " orders have delivered status in the source dataset.",
            sales.TotalUnits.ToString("N0") + " order items in the current CSV.",
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
        Text("MONTHLY REVENUE · LAST 12 MONTHS", new Vector3(-1.34f, .79f, .225f), .075f, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
        var allMonths = sales.MonthlySales.OrderBy(pair => pair.Key).ToArray();
        var months = allMonths.Skip(Math.Max(0, allMonths.Length - 12)).ToArray();
        if (months.Length == 0)
            months = new[] { new KeyValuePair<DateTime, double>(new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1), 0) };
        float x0 = -4.02f, x1 = 1.32f, baseY = -.57f, topY = .55f, max = NiceAxisMaximum((float)months.Select(pair => pair.Value).DefaultIfEmpty(0).Max());
        for (int tick = 0; tick <= 2; tick++)
        {
            float y = Mathf.Lerp(baseY, topY, tick / 2f);
            float value = max * tick / 2f;
            Box("Revenue Gridline · " + Money(value), new Vector3((x0 + x1) * .5f, y, .225f), x1 - x0, .012f, .025f, Grid, false);
            Text(Money(value), new Vector3(x0 - .04f, y, .245f), .041f, Muted, TextAnchor.MiddleRight);
        }
        Box("Revenue Axis · X", new Vector3((x0 + x1) * .5f, baseY, .245f), x1 - x0, .022f, .045f, Muted, false);
        Box("Revenue Axis · Y", new Vector3(x0, (baseY + topY) * .5f, .245f), .022f, topY - baseY, .045f, Muted, false);
        var points = new List<Vector3>();
        float step = months.Length <= 1 ? 0f : (x1 - x0) / (months.Length - 1f);
        for (int i = 0; i < months.Length; i++)
        {
            float x = x0 + i * step;
            float y = baseY + (topY - baseY) * (float)(months[i].Value / max);
            points.Add(new Vector3(x, y, .29f));
            Text(months[i].Key.ToString("MMM yy", CultureInfo.InvariantCulture), new Vector3(x, baseY - .16f, .26f), .041f, Muted, TextAnchor.MiddleCenter);
        }
        if (points.Count == 1) points.Add(new Vector3(x1, points[0].y, points[0].z));
        List<Vector3> smooth = SmoothPoints(points, 3);
        AreaMesh("Monthly Revenue Area · 3D Mesh", smooth, baseY, .255f, Hex("#D7EEDF"));
        for (int i = 0; i < smooth.Count - 1; i++) Segment("Monthly Revenue Curve · 3D", smooth[i], smooth[i + 1], .026f, Green);
        for (int i = 0; i < months.Length; i++) SphereClickable("Monthly Revenue Mark · " + months[i].Key.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            new Vector3(x0 + i * step, baseY + (topY - baseY) * (float)(months[i].Value / max), .29f), .042f, Bright,
            months[i].Key.ToString("MMMM yyyy", CultureInfo.InvariantCulture) + " item revenue: " + Money(months[i].Value) + ".");
        ChartFrame(-1.34f, .05f, 5.90f, 1.91f);
    }

    void BuildReminders()
    {
        Panel("Chart · Units per Category · 3D", new Vector3(3.12f, .05f, .12f), 2.88f, 1.91f, .12f, White, .10f);
        Text("UNITS PER CATEGORY", new Vector3(3.12f, .79f, .225f), .073f, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
        var leading = sales.CategoryUnits.Where(x => x.Value > 0).OrderByDescending(x => x.Value).Take(3).ToArray();
        var categories = leading.Select(x => new KeyValuePair<string, int>(x.Key, x.Value)).ToList();
        int otherUnits = sales.TotalUnits - leading.Sum(x => x.Value);
        if (otherUnits > 0) categories.Add(new KeyValuePair<string, int>("Other categories", otherUnits));
        categories = categories.OrderByDescending(category => category.Value).ToList();
        Color[] colors = { Green, GreenMid, GreenSoft, GreenPale };
        int total = Math.Max(1, sales.TotalUnits);
        float angle = -90f;
        for (int i = 0; i < categories.Count; i++)
        {
            string categoryName = categories[i].Key;
            int units = categories[i].Value;
            float share = (float)units / total;
            float end = angle + share * 360f;
            Donut(new Vector3(2.46f, .01f, .36f), .60f, .37f, .16f, angle, end, colors[i], categoryName);
            angle = end + 1.4f;
        }
        Text(CountShort(sales.TotalUnits), new Vector3(2.46f, .06f, .47f), .085f, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
        Text("UNITS", new Vector3(2.46f, -.11f, .47f), .039f, Muted, TextAnchor.MiddleCenter);
        for (int i = 0; i < categories.Count; i++)
        {
            string categoryName = categories[i].Key;
            int units = categories[i].Value;
            LegendDot(3.30f, .40f - i * .24f, colors[i], ShortCategory(categoryName) + " · " + CountShort(units));
        }
        ChartFrame(3.12f, .05f, 2.88f, 1.91f);
    }

    void BuildProjectList()
    {
        Panel("Chart · Revenue by Product · 3D", new Vector3(-1.72f, -1.70f, .12f), 5.38f, 1.40f, .12f, White, .10f);
        Text("TOTAL REVENUE", new Vector3(-4.23f, -1.08f, .225f), .071f, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
        Text("by product category", new Vector3(-4.23f, -1.20f, .225f), .046f, Muted, TextAnchor.MiddleLeft);
        var products = sales.SubcategorySales.OrderByDescending(x => x.Value).Take(8).ToArray();
        float max = (float)products.Select(product => product.Value).DefaultIfEmpty(1).Max();
        if (max <= 0) max = 1;
        Color[] palette = { Green, Green, GreenMid, GreenMid, GreenSoft, GreenSoft, GreenPale, GreenPale };
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
        float max = NiceAxisMaximum((float)cities.Select(city => city.Value).DefaultIfEmpty(0).Max());
        Color[] cityPalette = { Green, GreenMid, GreenMid, GreenSoft, GreenSoft, GreenPale, GreenPale };
        for (int tick = 0; tick <= 2; tick++)
        {
            float y = Mathf.Lerp(baseY, topY, tick / 2f);
            float value = max * tick / 2f;
            Box("City Revenue Gridline", new Vector3((x0 + x1) * .5f, y, .235f), x1 - x0, .012f, .025f, Grid, false);
            Text(Money(value), new Vector3(x0 - .05f, y, .25f), .036f, Muted, TextAnchor.MiddleRight);
        }
        Box("City Revenue X Axis", new Vector3((x0 + x1) * .5f, baseY, .25f), x1 - x0, .022f, .045f, Muted, false);
        Box("City Revenue Y Axis", new Vector3(x0, (baseY + topY) * .5f, .25f), .022f, topY - baseY, .045f, Muted, false);
        float step = cities.Length <= 1 ? x1 - x0 : (x1 - x0) / cities.Length;
        for (int i = 0; i < cities.Length; i++)
        {
            float x = x0 + step * (i + .5f);
            float h = (topY - baseY) * Mathf.Clamp01((float)cities[i].Value / max);
            Color barColor = cityPalette[i % cityPalette.Length];
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
        insightText = Text("LIVE DATA STATUS · EDIT LiveData/OlistSales.csv THEN PLAY", new Vector3(0, -2.49f, .18f), .038f, Muted, TextAnchor.MiddleCenter);
    }

    sealed class SalesSnapshot
    {
        public double TotalSales, TotalProfit;
        public int OrderCount, DeliveredOrders, CustomerCount, TotalUnits, RowCount;
        public float AverageUnitsPerOrder;
        public readonly SortedDictionary<DateTime, double> MonthlySales = new SortedDictionary<DateTime, double>();
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
        return ParseSalesSnapshot(ReadLiveData());
    }

    SalesSnapshot ParseSalesSnapshot(string csvText)
    {
        if (string.IsNullOrWhiteSpace(csvText)) throw new InvalidOperationException("The sales CSV is empty.");
        var result = new SalesSnapshot();
        string[] lines = csvText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) throw new InvalidOperationException("The sales CSV needs a header and at least one data row.");
        var headers = CsvLine(lines[0]);
        int orderCol = headers.IndexOf("Order ID"), dateCol = headers.IndexOf("Order Date");
        int regionCol = headers.IndexOf("Region"), categoryCol = headers.IndexOf("Category");
        int salesCol = headers.IndexOf("Sales");
        int customerCol = headers.IndexOf("Customer ID"), unitsCol = headers.IndexOf("Quantity");
        int shipCol = headers.IndexOf("Ship Date"), cityCol = headers.IndexOf("City"), subcategoryCol = headers.IndexOf("Sub-Category");
        int statusCol = headers.IndexOf("Order Status");
        int[] requiredColumns = { orderCol, dateCol, regionCol, categoryCol, salesCol, customerCol, unitsCol, shipCol, cityCol, subcategoryCol, statusCol };
        if (requiredColumns.Any(column => column < 0))
            throw new InvalidOperationException("Required columns: Order ID, Order Date, Ship Date, Customer ID, Region, Category, Sales, Quantity, City, Sub-Category, Order Status.");
        int lastRequiredColumn = requiredColumns.Max();
        var orders = new HashSet<string>(); var delivered = new HashSet<string>(); var customers = new HashSet<string>();
        CultureInfo dateCulture = CultureInfo.InvariantCulture;
        for (int row = 1; row < lines.Length; row++)
        {
            List<string> fields = CsvLine(lines[row]);
            if (fields.Count <= lastRequiredColumn) continue;
            if (!double.TryParse(fields[salesCol], NumberStyles.Float, CultureInfo.InvariantCulture, out double amount)) continue;
            result.RowCount++;
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
            {
                DateTime month = new DateTime(date.Year, date.Month, 1);
                if (!result.MonthlySales.ContainsKey(month)) result.MonthlySales[month] = 0;
                result.MonthlySales[month] += amount;
            }
        }
        if (result.RowCount == 0) throw new InvalidOperationException("No valid sales rows were found in the CSV.");
        result.OrderCount = orders.Count; result.DeliveredOrders = delivered.Count; result.CustomerCount = customers.Count;
        result.AverageUnitsPerOrder = result.OrderCount == 0 ? 0 : (float)result.TotalUnits / result.OrderCount;
        result.MaxMonthlySales = result.MonthlySales.Values.DefaultIfEmpty(0).Max();
        return result;
    }

    static float NiceAxisMaximum(float value)
    {
        if (value <= 0 || float.IsNaN(value) || float.IsInfinity(value)) return 1f;
        float roughStep = value / 2f;
        float magnitude = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(roughStep)));
        float fraction = roughStep / magnitude;
        float niceFraction = fraction <= 1f ? 1f : fraction <= 2f ? 2f : fraction <= 5f ? 5f : 10f;
        return niceFraction * magnitude * 2f;
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
            else if (c == ',' && !quoted) { fields.Add(field.ToString().Trim().TrimStart('\uFEFF')); field.Length = 0; }
            else field.Append(c);
        }
        fields.Add(field.ToString().Trim().TrimStart('\uFEFF')); return fields;
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
        generatedMeshes.Add(mesh);
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
        var mesh = new Mesh { name = "3D Portfolio Progress Arc" }; generatedMeshes.Add(mesh); mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
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
        var mesh=new Mesh{name="Rounded 3D Dashboard Panel"}; generatedMeshes.Add(mesh); mesh.SetVertices(vertices); mesh.SetTriangles(tris,0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }

    static Vector3 LayoutPosition(Vector3 p) => new Vector3(-p.x, p.y, p.z);
    Material Material(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(shader) { color = color };
        generatedMaterials.Add(m);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .04f);
        return m;
    }
}
