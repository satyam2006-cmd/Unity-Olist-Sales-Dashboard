using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.InputSystem;

/// <summary>A physical 3D earthquake dashboard driven by the public USGS weekly feed.</summary>
public sealed class QuakeScopeDashboard : MonoBehaviour
{
    const string FeedUrl = "https://earthquake.usgs.gov/earthquakes/feed/v1.0/summary/all_week.geojson";
    static readonly Color Background = new Color32(8, 17, 29, 255);
    static readonly Color Porcelain = new Color32(20, 34, 49, 255);
    static readonly Color Mint = new Color32(25, 53, 65, 255);
    static readonly Color MintDark = new Color32(28, 149, 151, 255);
    static readonly Color Green = new Color32(53, 211, 176, 255);
    static readonly Color Ink = new Color32(232, 241, 246, 255);
    static readonly Color Muted = new Color32(153, 175, 191, 255);
    static readonly Color Amber = new Color32(255, 185, 76, 255);
    static readonly Color Rose = new Color32(255, 105, 124, 255);
    static readonly Color Grid = new Color32(53, 75, 91, 255);

    readonly List<GameObject> markers = new List<GameObject>();
    readonly List<GameObject> bars = new List<GameObject>();
    readonly List<GameObject> donutParts = new List<GameObject>();
    readonly List<GameObject> latestLabels = new List<GameObject>();
    readonly List<Transform> floaters = new List<Transform>();
    readonly Dictionary<Collider, Action> actions = new Dictionary<Collider, Action>();
    readonly Dictionary<Collider, string> markerDetails = new Dictionary<Collider, string>();

    Transform board, globe;
    Camera sceneCamera;
    TextMesh eventValue, strongValue, averageValue, shallowValue, deepValue, maxValue;
    TextMesh statusText, filterText, selectionText, donutCenterText;
    Feature[] currentEvents;
    bool strongOnly, dragging, loading;
    Vector2 previousMouse;
    float zoom = 1f;
    int eventCount, strongCount;

    void Start()
    {
        Application.targetFrameRate = 60;
        board = transform.Find("QuakeScope · Floating Dashboard");
        if (board == null)
        {
            BuildEnvironment();
            BuildPhysicalLayout();
        }
        BindExistingScene();
        StartCoroutine(LoadFeed());
    }

    /// <summary>Creates a complete, editable preview hierarchy in the open scene.</summary>
    public void BuildInEditor()
    {
        if (Application.isPlaying) return;
        ClearGeneratedChildren();
        BuildEnvironment();
        BuildPhysicalLayout();
        statusText.text = "PREVIEW DATA · PLAY FOR LIVE USGS";
        RenderData(PreviewEvents());
    }

    void BindExistingScene()
    {
        if (board == null) board = transform.Find("QuakeScope · Floating Dashboard");
        if (board == null) return;

        sceneCamera = GetComponentInChildren<Camera>(true);
        globe = board.Find("3D Earth · Epicenter Globe");
        statusText = FindText("UI · Feed Status");
        eventValue = FindText("KPI Value · Events");
        strongValue = FindText("KPI Value · Magnitude 4+");
        averageValue = FindText("KPI Value · Mean Magnitude");
        shallowValue = FindText("KPI Value · Shallowest");
        deepValue = FindText("KPI Value · Deepest");
        maxValue = FindText("KPI Value · Strongest");
        selectionText = FindText("UI · Event Selection");
        filterText = FindText("UI · Filter Button");
        donutCenterText = FindText("Chart · Donut Center Label");

        for (int i = 0; i < board.childCount; i++)
        {
            var child = board.GetChild(i);
            string name = child.name;
            if (name == "Floating 3D Accent") floaters.Add(child);
            if (name.StartsWith("Latest Event ·", StringComparison.Ordinal)) latestLabels.Add(child.gameObject);
            if (name.StartsWith("3D Magnitude Column ·", StringComparison.Ordinal) ||
                (name.StartsWith("3D Text ·", StringComparison.Ordinal) && IsMagnitudeTickLabel(name)))
                bars.Add(child.gameObject);
            if (name == "3D Donut Segment" || name == "3D Donut Center") donutParts.Add(child.gameObject);
        }

        foreach (var collider in board.GetComponentsInChildren<Collider>(true))
        {
            if (collider.name.Contains("REFRESH")) actions[collider] = Refresh;
            else if (collider.name.Contains("SHOWING ALL")) actions[collider] = ToggleFilter;
            else if (collider.name.StartsWith("Earthquake ·", StringComparison.Ordinal))
            {
                markers.Add(collider.gameObject);
                markerDetails[collider] = collider.name;
            }
        }
    }

    TextMesh FindText(string objectName)
    {
        foreach (var textMesh in board.GetComponentsInChildren<TextMesh>(true))
            if (textMesh.gameObject.name == objectName) return textMesh;
        return null;
    }

    static bool IsMagnitudeTickLabel(string name)
    {
        string label = name.Substring("3D Text ·".Length).Trim();
        return label == "0" || label == "1" || label == "2" || label == "3" || label == "4" ||
               label == "5" || label == "6+";
    }

    void ClearGeneratedChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            if (child.name == "QuakeScope Camera" || child.name == "Softbox Key Light" || child.name == "Softbox Fill Light")
                continue;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
        board = null;
        globe = null;
        sceneCamera = null;
        markers.Clear(); bars.Clear(); donutParts.Clear(); latestLabels.Clear(); floaters.Clear();
        actions.Clear(); markerDetails.Clear();
    }

    Feature[] PreviewEvents()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return new[]
        {
            Preview(5.1f, -151.2f, 61.3f, 12f, "Alaska"),
            Preview(3.2f, -116.8f, 33.0f, 38f, "Southern California"),
            Preview(2.7f, -70.4f, -24.1f, 4f, "Northern Chile"),
            Preview(4.3f, 142.0f, 38.2f, 52f, "Near the east coast of Honshu"),
            Preview(1.8f, 12.3f, 42.1f, 8f, "Central Italy"),
            Preview(2.1f, 129.5f, -3.2f, 91f, "Papua, Indonesia"),
            Preview(3.7f, -155.3f, 19.4f, 7f, "Hawaii")
        };
        Feature Preview(float magnitude, float longitude, float latitude, float depth, string place) =>
            new Feature { properties = new Properties { mag = magnitude, place = place, time = now },
                geometry = new Geometry { coordinates = new[] { longitude, latitude, depth } } };
    }

    void Update()
    {
        if (globe != null)
        {
            if (!dragging) globe.Rotate(Vector3.up, 3.5f * Time.deltaTime, Space.World);
            globe.localScale = Vector3.Lerp(globe.localScale, Vector3.one * zoom, Time.deltaTime * 8f);
        }
        HandleMouse();
        for (int i = floaters.Count - 1; i >= 0; i--)
            if (floaters[i] != null) floaters[i].Rotate(0, 12f * Time.deltaTime, 8f * Time.deltaTime, Space.World);
    }

    void BuildEnvironment()
    {
        var cameraObject = transform.Find("QuakeScope Camera")?.gameObject;
        sceneCamera = cameraObject != null ? cameraObject.GetComponent<Camera>() : null;
        if (sceneCamera == null)
        {
            if (cameraObject != null)
            {
                if (Application.isPlaying) Destroy(cameraObject);
                else DestroyImmediate(cameraObject);
            }
            cameraObject = new GameObject("QuakeScope Camera", typeof(Camera));
            sceneCamera = cameraObject.GetComponent<Camera>();
        }
        cameraObject.name = "QuakeScope Camera";
        cameraObject.transform.SetParent(transform, false);
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0, 0, -8.6f);
        cameraObject.transform.LookAt(Vector3.zero);
        sceneCamera.clearFlags = CameraClearFlags.SolidColor;
        sceneCamera.backgroundColor = Background;
        sceneCamera.fieldOfView = 39;
        sceneCamera.nearClipPlane = .05f;

        RenderSettings.ambientLight = new Color(.48f, .56f, .64f);
        RenderSettings.fog = false;
        var keyObject = transform.Find("Softbox Key Light")?.gameObject;
        if (keyObject == null || keyObject.GetComponent<Light>() == null)
        {
            if (keyObject != null) { if (Application.isPlaying) Destroy(keyObject); else DestroyImmediate(keyObject); }
            keyObject = new GameObject("Softbox Key Light", typeof(Light));
        }
        keyObject.transform.SetParent(transform, false);
        var key = keyObject.GetComponent<Light>();
        key.type = LightType.Directional;
        key.intensity = 1.8f;
        key.shadows = LightShadows.Soft;
        key.transform.position = new Vector3(0f, 8f, -4f);
        key.transform.rotation = Quaternion.Euler(28, -25, 0);
        var fillObject = transform.Find("Softbox Fill Light")?.gameObject;
        if (fillObject == null || fillObject.GetComponent<Light>() == null)
        {
            if (fillObject != null) { if (Application.isPlaying) Destroy(fillObject); else DestroyImmediate(fillObject); }
            fillObject = new GameObject("Softbox Fill Light", typeof(Light));
        }
        fillObject.transform.SetParent(transform, false);
        var fill = fillObject.GetComponent<Light>();
        fill.type = LightType.Directional;
        fill.intensity = .8f;
        fill.color = new Color32(70, 210, 190, 255);
        fill.transform.position = new Vector3(0f, -8f, -4f);
        fill.transform.rotation = Quaternion.Euler(-25, 145, 0);
    }

    void BuildPhysicalLayout()
    {
        board = new GameObject("QuakeScope · Floating Dashboard").transform;
        board.SetParent(transform, false);
        board.position = new Vector3(0, 0, -2.35f);
        board.rotation = Quaternion.Euler(-2.2f, 180f, 0);
        Plate(board, "Soft Shadow", new Vector3(0, -.055f, -.13f), 6.94f, 3.96f, .12f, .30f, new Color32(7, 13, 22, 255));
        Plate(board, "Porcelain Console", Vector3.zero, 6.82f, 3.88f, .19f, .30f, Porcelain);

        // Raised physical sections establish the layout before their labels are placed.
        Plate(board, "Header Tile", new Vector3(0, 1.53f, .13f), 6.46f, .48f, .08f, .15f, Porcelain);
        Plate(board, "Weekly Events Tile", new Vector3(-2.13f, .94f, .14f), 2.00f, .58f, .10f, .13f, Mint);
        Plate(board, "Major Events Tile", new Vector3(0, .94f, .14f), 2.00f, .58f, .10f, .13f, Porcelain);
        Plate(board, "Mean Magnitude Tile", new Vector3(2.13f, .94f, .14f), 2.00f, .58f, .10f, .13f, Mint);
        Plate(board, "Globe Display Tile", new Vector3(-1.66f, -.12f, .135f), 2.67f, 1.73f, .10f, .16f, new Color32(14, 42, 54, 255));
        Plate(board, "Magnitude Chart Tile", new Vector3(1.57f, -.12f, .135f), 3.20f, 1.73f, .10f, .16f, new Color32(16, 35, 50, 255));
        Plate(board, "Shallow Event Tile", new Vector3(-2.13f, -1.34f, .14f), 2.00f, .42f, .10f, .12f, Porcelain);
        Plate(board, "Deep Event Tile", new Vector3(0, -1.34f, .14f), 2.00f, .42f, .10f, .12f, Mint);
        Plate(board, "Strongest Event Tile", new Vector3(2.13f, -1.34f, .14f), 2.00f, .42f, .10f, .12f, Porcelain);

        CreateEarth();
        globe.gameObject.SetActive(false);
        PlaceHeadingsAndValues();
        CreatePhysicalControls();
        CreateFloatingDecor();
    }

    void PlaceHeadingsAndValues()
    {
        statusText = WorldText(board, "CONNECTING TO USGS…", new Vector3(1.24f, 1.53f, .205f), .105f, Muted, TextAnchor.MiddleRight, FontStyle.Bold);
        statusText.gameObject.name = "UI · Feed Status";
        WorldText(board, "QUAKESCOPE", new Vector3(-3.08f, 1.55f, .205f), .19f, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
        WorldText(board, "GLOBAL SEISMIC ACTIVITY   ·   PAST 7 DAYS", new Vector3(-3.05f, 1.39f, .205f), .085f, Green, TextAnchor.MiddleLeft, FontStyle.Bold);

        WorldText(board, "EVENTS THIS WEEK", new Vector3(-2.98f, 1.02f, .215f), .077f, Muted, TextAnchor.MiddleLeft, FontStyle.Bold);
        eventValue = WorldText(board, "—", new Vector3(-2.98f, .82f, .215f), .19f, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
        eventValue.gameObject.name = "KPI Value · Events";
        WorldText(board, "MAGNITUDE 4+", new Vector3(-.86f, 1.02f, .215f), .077f, Muted, TextAnchor.MiddleLeft, FontStyle.Bold);
        strongValue = WorldText(board, "—", new Vector3(-.86f, .82f, .215f), .19f, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
        strongValue.gameObject.name = "KPI Value · Magnitude 4+";
        WorldText(board, "MEAN MAGNITUDE", new Vector3(1.27f, 1.02f, .215f), .077f, Muted, TextAnchor.MiddleLeft, FontStyle.Bold);
        averageValue = WorldText(board, "—", new Vector3(1.27f, .82f, .215f), .19f, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
        averageValue.gameObject.name = "KPI Value · Mean Magnitude";

        WorldText(board, "LATEST ACTIVITY", new Vector3(-.52f, .65f, .205f), .11f, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
        WorldText(board, "USGS · PAST 7 DAYS", new Vector3(-.52f, .48f, .205f), .067f, Muted, TextAnchor.MiddleLeft);
        selectionText = null;
        selectionText.gameObject.name = "UI · Event Selection";

        WorldText(board, "MAGNITUDE MIX", new Vector3(.08f, .62f, .205f), .11f, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
        WorldText(board, "EVENTS BY MAGNITUDE", new Vector3(1.25f, .62f, .205f), .075f, Muted, TextAnchor.MiddleLeft, FontStyle.Bold);
        WorldText(board, "MAGNITUDE DISTRIBUTION", new Vector3(.08f, -.70f, .215f), .085f, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);

        WorldText(board, "SHALLOWEST", new Vector3(-2.98f, -1.28f, .215f), .067f, Muted, TextAnchor.MiddleLeft, FontStyle.Bold);
        shallowValue = WorldText(board, "—", new Vector3(-2.98f, -1.46f, .215f), .12f, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
        shallowValue.gameObject.name = "KPI Value · Shallowest";
        WorldText(board, "DEEPEST", new Vector3(-.86f, -1.28f, .215f), .067f, Muted, TextAnchor.MiddleLeft, FontStyle.Bold);
        deepValue = WorldText(board, "—", new Vector3(-.86f, -1.46f, .215f), .12f, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
        deepValue.gameObject.name = "KPI Value · Deepest";
        WorldText(board, "STRONGEST", new Vector3(1.27f, -1.28f, .215f), .067f, Muted, TextAnchor.MiddleLeft, FontStyle.Bold);
        maxValue = WorldText(board, "—", new Vector3(1.27f, -1.46f, .215f), .12f, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
        maxValue.gameObject.name = "KPI Value · Strongest";

        WorldText(board, "LEGEND", new Vector3(-3.06f, -1.76f, .205f), .065f, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
        LegendMark(-2.30f, "M 0–1.9", new Color32(151, 211, 168, 255));
        LegendMark(-1.18f, "M 2–3.9", Green);
        LegendMark(.05f, "M 4–5.9", Amber);
        LegendMark(1.27f, "M 6+", Rose);
        WorldText(board, "USGS · LIVE", new Vector3(2.22f, -1.76f, .205f), .062f, Muted, TextAnchor.MiddleRight, FontStyle.Bold);
    }

    void LegendMark(float x, string label, Color color)
    {
        var chip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        chip.name = "3D Magnitude Legend Chip";
        chip.transform.SetParent(board, false);
        chip.transform.localPosition = LayoutPosition(new Vector3(x, -1.75f, .17f));
        chip.transform.localScale = new Vector3(.11f, .11f, .07f);
        chip.GetComponent<Renderer>().sharedMaterial = MakeMaterial(color, .5f);
        Destroy(chip.GetComponent<Collider>());
        WorldText(board, label, new Vector3(x + .09f, -1.75f, .21f), .059f, Ink, TextAnchor.MiddleLeft);
    }

    void CreateEarth()
    {
        globe = new GameObject("3D Earth · Epicenter Globe").transform;
        globe.SetParent(board, false);
        globe.localPosition = LayoutPosition(new Vector3(-1.66f, -.12f, .73f));
        var earth = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        earth.name = "Ocean Sphere";
        earth.transform.SetParent(globe, false);
        earth.transform.localScale = Vector3.one * 1.40f;
        earth.GetComponent<Renderer>().sharedMaterial = MakeMaterial(new Color32(26, 113, 128, 255), .72f);
        var earthCollider = earth.GetComponent<SphereCollider>();
        earthCollider.radius = .51f;
        CreateGlobeGrid(globe);
    }

    void CreatePhysicalControls()
    {
        CreateButton("↻  REFRESH", new Vector3(2.69f, 1.53f, .24f), 1.03f, .30f, Refresh);
        CreateButton("SHOWING ALL", new Vector3(-1.42f, 1.53f, .24f), 1.02f, .30f, ToggleFilter);
    }

    void CreateButton(string label, Vector3 center, float width, float height, Action callback)
    {
        var go = new GameObject("3D Button · " + label, typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider));
        go.transform.SetParent(board, false);
        go.transform.localPosition = LayoutPosition(center);
        go.GetComponent<MeshFilter>().sharedMesh = RoundedBoxMesh(width, height, .13f, .10f);
        go.GetComponent<MeshRenderer>().sharedMaterial = MakeMaterial(MintDark, .65f);
        var collider = go.GetComponent<BoxCollider>(); collider.size = new Vector3(width, height, 1.2f);
        actions[collider] = callback;
        var caption = WorldText(board, label, center + new Vector3(0, 0, .08f), .07f, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
        if (label.Contains("SHOWING ALL")) caption.gameObject.name = "UI · Filter Button";
        if (label == "SHOWING ALL") filterText = caption;
    }

    void CreateFloatingDecor()
    {
        AddDecor(PrimitiveType.Sphere, new Vector3(-3.70f, 1.93f, .15f), .24f, MintDark);
        AddDecor(PrimitiveType.Cube, new Vector3(3.75f, 1.44f, .28f), .19f, Porcelain);
        AddDecor(PrimitiveType.Sphere, new Vector3(3.72f, -1.65f, .24f), .28f, MintDark);
        AddDecor(PrimitiveType.Cube, new Vector3(-3.75f, -1.53f, .18f), .16f, Green);
    }

    void AddDecor(PrimitiveType type, Vector3 position, float size, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = "Floating 3D Accent"; go.transform.SetParent(board, false); go.transform.localPosition = LayoutPosition(position); go.transform.localScale = Vector3.one * size;
        go.GetComponent<Renderer>().sharedMaterial = MakeMaterial(color, .72f);
        Destroy(go.GetComponent<Collider>()); floaters.Add(go.transform);
    }

    TextMesh WorldText(Transform parent, string value, Vector3 localPosition, float size, Color color,
        TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
    {
        var go = new GameObject("3D Text · " + value);
        go.transform.SetParent(parent, false); go.transform.localPosition = LayoutPosition(localPosition);
        go.transform.localRotation = Quaternion.identity;
        var mesh = go.AddComponent<TextMesh>();
        mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        mesh.fontSize = 100; mesh.characterSize = size * .1f;
        mesh.anchor = anchor;
        mesh.alignment = TextAlignment.Left;
        mesh.fontStyle = style; mesh.color = color; mesh.text = value; mesh.richText = false;
        return mesh;
    }

    void Plate(Transform parent, string objectName, Vector3 localPosition, float width, float height,
        float depth, float radius, Color color)
    {
        var go = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false); go.transform.localPosition = LayoutPosition(localPosition);
        go.GetComponent<MeshFilter>().sharedMesh = RoundedBoxMesh(width, height, depth, radius);
        go.GetComponent<MeshRenderer>().sharedMaterial = MakeMaterial(color, .12f);
    }

    Mesh RoundedBoxMesh(float width, float height, float depth, float radius)
    {
        const int stepsPerCorner = 8;
        var ring = new List<Vector2>();
        Vector2[] centers = {
            new Vector2(width/2-radius,height/2-radius), new Vector2(-width/2+radius,height/2-radius),
            new Vector2(-width/2+radius,-height/2+radius), new Vector2(width/2-radius,-height/2+radius)
        };
        for (int c=0;c<4;c++)
            for (int i=0;i<=stepsPerCorner;i++)
            {
                float a=(c*90f+i*90f/stepsPerCorner)*Mathf.Deg2Rad;
                ring.Add(centers[c]+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius);
            }
        int n=ring.Count, backCenter=n+1, backRing=backCenter+1;
        var vertices=new List<Vector3>{Vector3.zero};
        for(int i=0;i<n;i++)vertices.Add(new Vector3(ring[i].x,ring[i].y,depth/2));
        vertices.Add(new Vector3(0,0,-depth/2));
        for(int i=0;i<n;i++)vertices.Add(new Vector3(ring[i].x,ring[i].y,-depth/2));
        var tris=new List<int>();
        for(int i=0;i<n;i++)
        {
            int j=(i+1)%n; tris.Add(0);tris.Add(i+1);tris.Add(j+1);
            tris.Add(backCenter);tris.Add(backRing+j);tris.Add(backRing+i);
            tris.Add(i+1);tris.Add(backRing+i);tris.Add(j+1);
            tris.Add(j+1);tris.Add(backRing+i);tris.Add(backRing+j);
        }
        var mesh=new Mesh{name="Rounded Beveled 3D Panel"};mesh.SetVertices(vertices);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        return mesh;
    }

    void CreateGlobeGrid(Transform parent)
    {
        for(int lat=-60;lat<=60;lat+=30) CreateGridLine(parent,lat,true);
        for(int lon=0;lon<180;lon+=30) CreateGridLine(parent,lon,false);
    }

    void CreateGridLine(Transform parent,int angle,bool latitude)
    {
        var go=new GameObject(latitude?"Latitude Ring":"Longitude Ring");go.transform.SetParent(parent,false);
        var line=go.AddComponent<LineRenderer>();line.useWorldSpace=false;line.loop=false;line.positionCount=97;
        line.startWidth=line.endWidth=.008f;line.material=MakeMaterial(new Color32(78, 182, 177, 255),.3f);
        for(int i=0;i<97;i++)
        {
            float t=i*Mathf.PI*2/96f;
            float lat=latitude?angle*Mathf.Deg2Rad:-Mathf.PI/2+t;
            float lon=latitude?t:angle*Mathf.Deg2Rad;
            line.SetPosition(i,Geo(lat,lon,.715f));
        }
    }

    IEnumerator LoadFeed()
    {
        if(loading)yield break;loading=true;
        statusText.text="SYNCING WITH USGS…";
        using(var request=UnityWebRequest.Get(FeedUrl))
        {
            request.timeout=20;yield return request.SendWebRequest();
            if(request.result!=UnityWebRequest.Result.Success)
            {
                statusText.text="USGS CONNECTION FAILED · CLICK REFRESH";loading=false;yield break;
            }
            var feed=JsonUtility.FromJson<Feed>(request.downloadHandler.text);
            if(feed==null||feed.features==null||feed.features.Length==0)
            {statusText.text="NO EVENTS RETURNED";loading=false;yield break;}
            RenderData(feed.features);
        }
        statusText.text="LIVE USGS DATA · UPDATED "+DateTime.Now.ToString("HH:mm");loading=false;
    }

    void RenderData(Feature[] features)
    {
        currentEvents=features;eventCount=0;strongCount=0;
        int[] bins=new int[7];float sum=0,shallow=float.MaxValue,deep=0,maxMag=-1;
        var latest=new List<Feature>();
        foreach(var e in features)
        {
            if(e.properties==null||e.geometry==null||e.geometry.coordinates==null||e.geometry.coordinates.Length<3||e.properties.mag<0)continue;
            float mag=e.properties.mag,depth=Mathf.Max(0,e.geometry.coordinates[2]);
            eventCount++;sum+=mag;bins[Mathf.Clamp(Mathf.FloorToInt(mag),0,6)]++;
            if(mag>=4)strongCount++;
            shallow=Mathf.Min(shallow,depth);deep=Mathf.Max(deep,depth);maxMag=Mathf.Max(maxMag,mag);
            if(latest.Count<3)latest.Add(e);
        }
        eventValue.text=eventCount.ToString("N0");strongValue.text=strongCount.ToString("N0");averageValue.text=(eventCount>0?sum/eventCount:0).ToString("0.0");
        shallowValue.text=shallow.ToString("0")+" km";deepValue.text=deep.ToString("0")+" km";maxValue.text="M "+maxMag.ToString("0.0");
        foreach(var label in latestLabels)if(label!=null)Destroy(label);latestLabels.Clear();
        for(int i=0;i<latest.Count;i++)
        {
            var e=latest[i];
            var label=WorldText(board,"M "+e.properties.mag.ToString("0.0")+"   "+TrimPlace(e.properties.place)+"   ·   "+Relative(e.properties.time)+" ago",
                new Vector3(-.52f,.28f-i*.36f,.23f),.061f,i==0?Green:Muted);
            label.gameObject.name="Latest Event · "+(i+1);
            latestLabels.Add(label.gameObject);
        }
        BuildMagnitudeColumns(bins);BuildMagnitudeDonut(bins);ApplyMarkerFilter();
    }

    void BuildMagnitudeColumns(int[] bins)
    {
        foreach(var go in bars)if(go!=null)Destroy(go);bars.Clear();
        int max=1;foreach(int n in bins)max=Mathf.Max(max,n);
        var oldAxes=board.Find("3D Chart Axes");
        if(oldAxes!=null){if(Application.isPlaying)Destroy(oldAxes.gameObject);else DestroyImmediate(oldAxes.gameObject);}
        var axes=new GameObject("3D Chart Axes").transform;axes.SetParent(board,false);
        Plate(axes,"3D Chart X Axis",new Vector3(2.12f,-.48f,.24f),1.78f,.025f,.085f,.012f,Muted);
        Plate(axes,"3D Chart Y Axis",new Vector3(1.25f,-.18f,.24f),.025f,.60f,.085f,.012f,Muted);
        int[] ticks={0,Mathf.Max(1,max/2),max};
        for(int i=0;i<3;i++)
        {
            float y=-.48f+i*.30f;
            Plate(axes,"3D Chart Gridline · "+ticks[i],new Vector3(2.12f,y,.215f),1.72f,.012f,.035f,.005f,Grid);
            Plate(axes,"3D Chart Tick",new Vector3(1.29f,y,.27f),.095f,.018f,.055f,.008f,Green);
            WorldText(axes,ticks[i].ToString("N0"),new Vector3(1.17f,y,.285f),.047f,Muted,TextAnchor.MiddleRight);
        }
        string[] labels={"0","1","2","3","4","5","6+"};
        for(int i=0;i<7;i++)
        {
            float h=.045f+.50f*Mathf.Sqrt((float)bins[i]/max),x=1.53f+i*.205f;
            Color color=i>=6?Rose:i>=4?Amber:i>=2?Green:MintDark;
            var bar=GameObject.CreatePrimitive(PrimitiveType.Cube);bar.name="3D Magnitude Column · "+labels[i];
            bar.transform.SetParent(board,false);bar.transform.localPosition=LayoutPosition(new Vector3(x,-.455f+h/2,.32f));
            bar.transform.localScale=new Vector3(.13f,h,.16f);bar.GetComponent<Renderer>().sharedMaterial=MakeMaterial(color,.58f);
            Destroy(bar.GetComponent<Collider>());bars.Add(bar);
            bars.Add(WorldText(board,labels[i],new Vector3(x,-.57f,.285f),.054f,Muted,TextAnchor.MiddleCenter).gameObject);
        }
    }

    void BuildMagnitudeDonut(int[] bins)
    {
        foreach(var go in donutParts)if(go!=null)Destroy(go);donutParts.Clear();
        int[] values={bins[0]+bins[1],bins[2]+bins[3],bins[4]+bins[5],bins[6]};
        int total=0;foreach(int n in values)total+=n;if(total==0)return;
        Color[] colors={MintDark,Green,Amber,Rose};
        float start=-90f;Vector3 center=new Vector3(.56f,.02f,.34f);
        for(int i=0;i<values.Length;i++)
        {
            if(values[i]==0)continue;
            float end=start+360f*values[i]/total;
            donutParts.Add(CreateDonutSegment(center,.47f,.245f,.13f,start,end,colors[i]));start=end;
        }
        var hub=GameObject.CreatePrimitive(PrimitiveType.Cylinder);hub.name="3D Donut Center";hub.transform.SetParent(board,false);
        hub.transform.localPosition=LayoutPosition(center);hub.transform.localRotation=Quaternion.Euler(90,0,0);hub.transform.localScale=new Vector3(.46f,.055f,.46f);
        hub.GetComponent<Renderer>().sharedMaterial=MakeMaterial(Porcelain,.82f);Destroy(hub.GetComponent<Collider>());donutParts.Add(hub);
        if(donutCenterText==null){donutCenterText=WorldText(board,"",new Vector3(.56f,.04f,.47f),.063f,Ink,TextAnchor.MiddleCenter,FontStyle.Bold);donutCenterText.gameObject.name="Chart · Donut Center Label";}
        donutCenterText.text=eventCount.ToString("N0")+"\nEVENTS";
    }

    GameObject CreateDonutSegment(Vector3 center,float outer,float inner,float thickness,float start,float end,Color color)
    {
        int steps=Mathf.Max(3,Mathf.CeilToInt((end-start)/6f));float half=thickness/2;
        var vertices=new List<Vector3>();var triangles=new List<int>();
        for(int i=0;i<=steps;i++)
        {
            float a=Mathf.Lerp(start,end,(float)i/steps)*Mathf.Deg2Rad;
            Vector2 o=new Vector2(Mathf.Cos(a)*outer,Mathf.Sin(a)*outer),n=new Vector2(Mathf.Cos(a)*inner,Mathf.Sin(a)*inner);
            vertices.Add(new Vector3(o.x,o.y,half));vertices.Add(new Vector3(n.x,n.y,half));
            vertices.Add(new Vector3(o.x,o.y,-half));vertices.Add(new Vector3(n.x,n.y,-half));
        }
        for(int i=0;i<steps;i++)
        {
            int a=i*4,b=a+4;
            triangles.Add(a);triangles.Add(b);triangles.Add(a+1);triangles.Add(a+1);triangles.Add(b);triangles.Add(b+1);
            triangles.Add(a+2);triangles.Add(a+3);triangles.Add(b+2);triangles.Add(a+3);triangles.Add(b+3);triangles.Add(b+2);
            triangles.Add(a);triangles.Add(a+2);triangles.Add(b);triangles.Add(b);triangles.Add(a+2);triangles.Add(b+2);
            triangles.Add(a+1);triangles.Add(b+1);triangles.Add(a+3);triangles.Add(b+1);triangles.Add(b+3);triangles.Add(a+3);
        }
        var mesh=new Mesh{name="Solid 3D Donut Slice"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        var go=new GameObject("3D Donut Segment",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(board,false);go.transform.localPosition=LayoutPosition(center);
        go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=MakeMaterial(color,.62f);return go;
    }

    void ApplyMarkerFilter()
    {
        foreach(var marker in markers)if(marker!=null)Destroy(marker);markers.Clear();markerDetails.Clear();
        if(currentEvents==null)return;
        int stride=Mathf.Max(1,currentEvents.Length/280),index=0,shown=0;
        foreach(var e in currentEvents)
        {
            if(e.properties==null||e.geometry==null||e.geometry.coordinates==null||e.geometry.coordinates.Length<3)continue;
            if(strongOnly&&e.properties.mag<4)continue;
            index++;if(index%stride!=0||shown>=280)continue;
            AddEarthquakeMarker(e);shown++;
        }
    }

    void AddEarthquakeMarker(Feature e)
    {
        float lon=e.geometry.coordinates[0],lat=e.geometry.coordinates[1],depth=Mathf.Max(0,e.geometry.coordinates[2]),mag=e.properties.mag;
        float radius=Mathf.Clamp(.715f-depth/1600f,.49f,.715f);
        var marker=GameObject.CreatePrimitive(PrimitiveType.Sphere);marker.name="Earthquake · M"+mag.ToString("0.0");marker.transform.SetParent(globe,false);
        marker.transform.localPosition=Geo(lat*Mathf.Deg2Rad,lon*Mathf.Deg2Rad,radius);
        float size=Mathf.Lerp(.028f,.12f,Mathf.InverseLerp(1,6.5f,mag));marker.transform.localScale=Vector3.one*size;
        Color color=mag>=6?Rose:mag>=4?Amber:mag>=2?Green:MintDark;
        marker.GetComponent<Renderer>().sharedMaterial=MakeMaterial(color,.78f);
        var collider=marker.GetComponent<SphereCollider>();collider.radius=2.4f;
        markerDetails[collider]="M "+mag.ToString("0.0")+"   ·   "+e.properties.place+"\nDepth "+depth.ToString("0.0")+" km   ·   "+Relative(e.properties.time)+" ago";
        markers.Add(marker);
    }

    void HandleMouse()
    {
        if(Mouse.current==null)return;var mouse=Mouse.current;Vector2 position=mouse.position.ReadValue();
        if(mouse.scroll.ReadValue().y!=0)zoom=Mathf.Clamp(zoom+mouse.scroll.ReadValue().y*.00075f,.78f,1.30f);
        if(mouse.leftButton.wasPressedThisFrame)
        {
            Ray ray=sceneCamera.ScreenPointToRay(position);
            if(Physics.Raycast(ray,out var hit,100f))
            {
                if(actions.TryGetValue(hit.collider,out var action)){action.Invoke();return;}
                if(markerDetails.TryGetValue(hit.collider,out var detail)){selectionText.text=detail;return;}
                if(hit.collider.gameObject.name=="Ocean Sphere"){dragging=true;previousMouse=position;return;}
            }
        }
        if(mouse.leftButton.isPressed&&dragging)
        {
            Vector2 delta=position-previousMouse;
            globe.Rotate(Vector3.up,delta.x*.30f,Space.World);
            globe.Rotate(sceneCamera.transform.right,-delta.y*.30f,Space.World);
            previousMouse=position;
        }
        if(mouse.leftButton.wasReleasedThisFrame)dragging=false;
    }

    void Refresh()=>StartCoroutine(LoadFeed());
    void ToggleFilter()
    {
        strongOnly=!strongOnly;filterText.text=strongOnly?"FILTER: M 4+":"SHOWING ALL";ApplyMarkerFilter();
    }

    static Vector3 LayoutPosition(Vector3 local)=>new Vector3(-local.x,local.y,local.z);
    static Vector3 Geo(float lat,float lon,float radius)=>new Vector3(-radius*Mathf.Cos(lat)*Mathf.Sin(lon),radius*Mathf.Sin(lat),radius*Mathf.Cos(lat)*Mathf.Cos(lon));
    static string TrimPlace(string value)=>string.IsNullOrEmpty(value)?"Unknown location":value.Length>35?value.Substring(0,32)+"…":value;
    static string Relative(long milliseconds)
    {
        var span=DateTime.UtcNow-DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime;
        return span.TotalHours<1?Mathf.Max(1,(int)span.TotalMinutes)+" min":span.TotalDays<1?(int)span.TotalHours+" hr":(int)span.TotalDays+" day";
    }
    static Material MakeMaterial(Color color,float smoothness)
    {
        var shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
        var material=new Material(shader);material.color=color;
        if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",smoothness);
        return material;
    }

    [Serializable] class Feed { public Feature[] features; }
    [Serializable] class Feature { public Properties properties; public Geometry geometry; }
    [Serializable] class Properties { public float mag; public string place; public long time; }
    [Serializable] class Geometry { public float[] coordinates; }
}
