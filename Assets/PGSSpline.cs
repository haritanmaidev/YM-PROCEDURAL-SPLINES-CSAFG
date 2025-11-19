using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class PGSSpline : MonoBehaviour
{
    private LineRenderer line;

    [Header("Catmull Rom Spline Settings")]
    public Transform[] handles;
    public bool loopedSpline;
    [Range(2, 200)] public int totalSplinePoints = 20;
    [Range(1, 5)] public int segmentResolution = 1; //Resolution per each "catmull curve segment"
    public bool limitSplineDistance = false;
    [Range(1, 100f)] public float maxSplineDistance = 10f;
    [Range(1, 3)] public float splineLengthMultiplier = 1f;

    public Vector3[] generatedPoints;

    [Header("Spline Curvature Settings")]
    [Range(-10, 10f)] public float horizontalCurvature = 0f;
    [Range(-10, 10f)] public float verticalCurvature = 0f;
    [Range(0, 1)] public float curvatureArcWeightInfluence = 1f;

    [Header("Spiral Settings")]
    public bool enableSpiral = false;
    [Range(0f, 5f)] public float spiralRadius = 1f;
    [Range(0f, 10f)] public float spiralTurns = 1f;

    [Header("Perlin Noise Settings")]
    public bool enableNoise = false;
    [Range(0.01f, 20f)] public float noiseAmplitude = 1f;
    [Range(0.001f, 1f)] public float noiseFrequency = 1f;
    [Range(-100f, 100f)] public float noiseOffsetX = 0f;
    [Range(-100f, 100f)] public float noiseOffsetY = 0f;
    [Range(1, 3)] public int octaves = 0;
    [Range(0.25f, 10f)] public float exponent = 1f; //0.25 because anything below that is barely noticable
    private float lacuarity;

    [Header("Bend Setting")]
    public bool enableBending = true;
    public bool useEvenSpacing = true;
    public bool bendAway = true;
    public LayerMask bendLayer = 3;
    [Range(1f, 20f)] public float bendRadius = 5f;
    [Range(1f, 5f)] public float bendStrength = 1f;
    public int maxPhysicsTargetsPerPoint = 4;
    private const float bendClamp = 0.01f; //Explain well
    private float lastBendSide = 1f;
    private bool hasLastBendSide = false;
    private Vector3 currentSide = Vector3.right;
    private Vector3 currentDir = Vector3.forward;

    [Header("Gizmos Settings")]
    public bool enableGizmoAxes = false;
    [Range(0.1f, 1f)] public float gizmoAxesLength = 1f;
    public bool enableGizmoPoints = false;
    [Range(0.01f, 0.1f)] public float gizmoSphereRadius = 0.065f;

    //TODO: User can manually decide which 
    //FIX: CURVE WEIGHT ARC NEEDS TO BE FIXED, IT EFFECTS THINGS OUTSIDE CURVATURE SECTION
    //CONSIDER: Objects that will always attract or repel

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
    }

    private void Update()
    {
        BuildSplineFromHandles();
        DrawLineOnCatmullRomProceduralSpline(generatedPoints); 
        //NOTE: NO NULL CHECK HERE BECAUSE POINTS WILL ALWAYS BE MORE THAN 4
    }

    private void BuildSplineFromHandles() 
    {
        if (handles == null || handles.Length < 2) return;

        Vector3 A = handles[0].position;
        Vector3 B = handles[1].position;

        generatedPoints = GenerateControlPointsFromAtoB(A, B, totalSplinePoints);

        if (enableBending && useEvenSpacing && generatedPoints != null) 
        {
            RedistributePointsEvenlyAlongCurve(ref generatedPoints);
        }
    }

    private Vector3[] GenerateControlPointsFromAtoB(Vector3 A, Vector3 B, int internalPointCount)
    {
        Vector3 AB = B - A;
        Vector3 dir = AB.normalized;
        Vector3 side = Vector3.Cross(dir, Vector3.up).normalized;  // Sideways direction  
        Vector3 up = Vector3.Cross(side, dir).normalized; //Local up per point
        float distance = AB.magnitude;

        //To fix the bending becomes upon intersecting with a target object bug
        currentDir = dir;
        currentSide = side;

        //Build array of points: A/Start + internal/spline points + B/End
        int totalPointsLength = totalSplinePoints + 2;
        Vector3[] points = new Vector3[totalPointsLength];
        points[0] = A;
        points[totalPointsLength - 1] = B;

        //WHY
        //generatedPoints = new Vector3[totalPointsLength];
        //generatedPoints[0] = A;
        //generatedPoints[totalPointsLength - 1] = B;

        //Extending the line AB based on pathLengthMultiplier
        float extra = distance * (splineLengthMultiplier - 1f) * 0.5f;
        Vector3 extendedA = A - dir * extra;
        Vector3 extendedB = B + dir * extra;
        float extendedDistance = (extendedB - extendedA).magnitude;

        //Positioning the internal spline points between A and B. -2 to get the catmull rom based point not the actua final point
        for (int p = 1; p <= totalPointsLength - 2; p++) 
        {
            //INTIAL POSITION OF THE POINT
            float t = p / (float)(totalPointsLength - 1);
            Vector3 currentPointOnLine = extendedA + dir * (extendedDistance * t);

            //CURVATURE LOGIC
            float arcWeightFactor = Mathf.Sin(Mathf.PI * t) * curvatureArcWeightInfluence; //Sin90 = 0
            Vector3 curvatureOffset = (side * horizontalCurvature * arcWeightFactor) + (Vector3.up * verticalCurvature * arcWeightFactor);
            //WHY: should I use the splines up instead of vector up here?

            //SPIRAL LOGIC
            Vector3 spiralOffset = Vector3.zero;
            if (enableSpiral)
            {
                Vector3 radialDir = side;
                float angle = t * Mathf.PI * 2f * spiralTurns;
                radialDir = (side * Mathf.Cos(angle)) + (Vector3.up * Mathf.Sin(angle));
                float radius = spiralRadius * arcWeightFactor; 
                spiralOffset = radialDir * radius;
            }

            //PERLIN NOISE LOGIC
            Vector3 noiseOffset = Vector3.zero;
            if (enableNoise) 
            {
                float noise = Mathf.PerlinNoise(currentPointOnLine.x * noiseFrequency + noiseOffsetX, currentPointOnLine.y * noiseFrequency + noiseOffsetY) * noiseAmplitude;
                noiseOffset.x = noise;
                noiseOffset.y = noise;
            }

            //Final offset position before bending logic
            Vector3 basePos = currentPointOnLine + curvatureOffset + spiralOffset + noiseOffset;

            //BENDING TO AVOID OR BE ATTRACTED LOGIC
            Vector3 targetOffset = Vector3.zero;
            if (enableBending) 
            {
                //TODO: If object is practically supposed to touch the spline, exaggerated the bend a little since the current logic lead to bending through the object.
                //TODO: If both are at the same height, choose a random up or down dir to bend towards/away from

                Collider[] cols = Physics.OverlapSphere(basePos, bendRadius, bendLayer);
                int maxCollisions = Mathf.Min(cols.Length, maxPhysicsTargetsPerPoint); //Max objects influecing the spline

                for (int i = 0; i < maxCollisions; i++)
                {
                    Collider currentCol = cols[i];
                    targetOffset += ComputeColliderBendOffset(basePos, currentCol, arcWeightFactor, A, B);
                }
            }

            points[p] = basePos + targetOffset;
        }

        return points;
    }

    Vector3 ComputeColliderBendOffset(Vector3 splinePoint, Collider col, float archWeightFactor, Vector3 A, Vector3 B)
    {
        if (col == null) 
        {
            Debug.LogWarning("Collider passed into ComputeColliderBendOffset is null!");
            return Vector3.zero;
        }

        //Gets closest point on object's surface
        Vector3 closest = col.ClosestPoint(splinePoint);
        Vector3 toTarget = closest - splinePoint;
        float distanceToTarget = toTarget.magnitude;
        if (distanceToTarget >= bendRadius) return Vector3.zero;

        //Choosing bend direction
        Vector3 bendDir;

        //Case 1: Collider is not intersecting the spline
        if (distanceToTarget > bendClamp)
        {
            bendDir = toTarget / distanceToTarget;
        }
        //Case 2: Object colider intersects the AB line, so force a sideways bend dir based on which side the object came from
        else 
        {
            Vector3 ABdir = (B - A).normalized;
            Vector3 objCenter = col.bounds.center;
            //WHAT:
            float t = Vector3.Dot(objCenter - A, ABdir);
            Vector3 linePoint = A + ABdir * t;
            Vector3 fromLine = objCenter - linePoint;
            if (fromLine.sqrMagnitude < 0.0001f) fromLine = Vector3.Cross(ABdir, Vector3.up);
            bendDir = fromLine.normalized;
        }

        if (bendAway) bendDir = -bendDir;

        float bend = 1f - (distanceToTarget / bendRadius);//LEARN
        float weight = bend * bend * archWeightFactor;

        return bendDir * bendStrength * weight;
    }

    private void RedistributePointsEvenlyAlongCurve(ref Vector3[] points)
    {
        int pointCount = points.Length;

        //STEP 1: Dense samples along current Catmull curve
        const int denseSamplesPerCurve = 32; // per Catmull segment
        List<Vector3> densePoints = new List<Vector3>();

        int numCurves = pointCount - 3;

        for (int i = 0; i < numCurves; i++)
        {
            Vector3 p0 = points[i];
            Vector3 p1 = points[i + 1];
            Vector3 p2 = points[i + 2];
            Vector3 p3 = points[i + 3];

            for (int s = 0; s <= denseSamplesPerCurve; s++)
            {
                float t = s / (float)denseSamplesPerCurve;
                Vector3 pos = CatmullRom(p0, p1, p2, p3, t);

                // Avoid duplicating the first sample of each segment except the first one
                if (i > 0 && s == 0)
                    continue;

                densePoints.Add(pos);
            }
        }

        int denseCount = densePoints.Count;
        if (denseCount < 2)
            return;

        //STEP 2: Cumulative distances
        float[] cumLen = new float[denseCount];
        cumLen[0] = 0f;

        for (int i = 1; i < denseCount; i++)
        {
            float segLen = Vector3.Distance(densePoints[i], densePoints[i - 1]);
            cumLen[i] = cumLen[i - 1] + segLen;
        }

        float totalLen = cumLen[denseCount - 1];
        if (totalLen <= 0f)
            return;

        //STEP 3: New points at equal distance intervals
        Vector3[] newPoints = new Vector3[pointCount];
        int denseIndex = 0;

        for (int j = 0; j < pointCount; j++)
        {
            float targetDist = (j / (float)(pointCount - 1)) * totalLen;

            while (denseIndex < denseCount - 2 && cumLen[denseIndex + 1] < targetDist)
            {
                denseIndex++;
            }

            float segmentStart = cumLen[denseIndex];
            float segmentEnd = cumLen[denseIndex + 1];
            float segmentLen = segmentEnd - segmentStart;

            float u = 0f;
            if (segmentLen > 0f)
                u = (targetDist - segmentStart) / segmentLen;

            Vector3 a = densePoints[denseIndex];
            Vector3 b = densePoints[denseIndex + 1];

            newPoints[j] = Vector3.Lerp(a, b, u);
        }

        //Since this is catmull curve, passing the internal points will cause it to redistribute from p2 rather than p1. Caused spline to have 1 segment missing at A and B
        Vector3 start = points[0]; //Old A
        Vector3 end = points[pointCount - 1]; //Old B

        points = newPoints;

        points[0] = start;
        points[pointCount - 1] = end;
    }

    private void DrawLineOnCatmullRomProceduralSpline(Vector3[] points)
    {
        int totalCatmullCurves = points.Length - 3;
        int totalSegments = totalCatmullCurves * segmentResolution;
        line.positionCount = totalSegments + 1;

        //Iterating through points and storing positions of each point per each curve segment 
        int index = 0;
        for (int i = 0; i < totalCatmullCurves; i++)
        {
            Vector3 p0 = points[i];
            Vector3 p1 = points[i + 1];
            Vector3 p2 = points[i + 2];
            Vector3 p3 = points[i + 3];

            //Evaluate t on each catmull's curve segment based on total segment/resolution count per each curve segment
            for (int s = 0; s < segmentResolution; s++)
            {
                float t = s / (float)segmentResolution;
                line.SetPosition(index, CatmullRom(p0, p1, p2, p3, t));
                index++;
            }
        }

        Vector3 lp0 = points[points.Length - 4];
        Vector3 lp1 = points[points.Length - 3];
        Vector3 lp2 = points[points.Length - 2];
        Vector3 lp3 = points[points.Length - 1];

        line.SetPosition(index, CatmullRom(lp0, lp1, lp2, lp3, 1f));
    }

    private Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        // Calculate the Catmull-Rom spline point at parameter t
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * ((2f * p1) +
                       (-p0 + p2) * t +
                       (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                       (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private void OnDrawGizmos()
    {
        // Decide which set of points to visualize

        BuildSplineFromHandles();
        Vector3[] pts = generatedPoints;

        //DRAW LINE BETWEEN POINTS
        Gizmos.color = Color.white;
        int numCurves = pts.Length - 3;
        if (numCurves <= 0) return;

        Vector3 previousPos = CatmullRom(pts[0], pts[1], pts[2], pts[3], 0f);
        for (int i = 0; i < numCurves; i++)
        {
            Vector3 p0 = pts[i];
            Vector3 p1 = pts[i + 1];
            Vector3 p2 = pts[i + 2];
            Vector3 p3 = pts[i + 3];

            for (int s = 1; s <= segmentResolution; s++)
            {
                float t = s / (float)segmentResolution;
                Vector3 pos = CatmullRom(p0, p1, p2, p3, t);

                Gizmos.DrawLine(previousPos, pos);
                previousPos = pos;
            }
        }

        //DRAW SPHERES AT EACH POINT
        if (enableGizmoPoints)
        {
            Gizmos.color = Color.yellow;

            for (int i = 0; i < pts.Length; i++)
            {
                Gizmos.DrawSphere(pts[i], gizmoSphereRadius);
            }

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(pts[0], gizmoSphereRadius * 1.5f); //p0 start point
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(pts[1], gizmoSphereRadius * 1.5f); //p1 starting spline point
            int lastP = pts.Length - 2;
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(pts[lastP], gizmoSphereRadius * 1.5f); //pN - 2 ending spline point
            int endP = pts.Length - 1;
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(pts[endP], gizmoSphereRadius * 1.5f); //pN - 1 end point
        }

        //DRAW POINTS XYZ AXES
        if (enableGizmoAxes) 
        {
            for (int i = 0; i < pts.Length; i++)
            {
                Vector3 currentPoint = pts[i];
                Vector3 xDir = new Vector3(currentPoint.x + gizmoAxesLength, currentPoint.y, currentPoint.z);
                Vector3 yDir = new Vector3(currentPoint.x, currentPoint.y + gizmoAxesLength, currentPoint.z);
                Vector3 zDir = new Vector3(currentPoint.x, currentPoint.y, currentPoint.z + gizmoAxesLength);

                Gizmos.color = Color.red;
                Gizmos.DrawLine(currentPoint, xDir);

                Gizmos.color = Color.green;
                Gizmos.DrawLine(currentPoint, yDir);

                Gizmos.color = Color.blue;
                Gizmos.DrawLine(currentPoint, zDir);
            }
        }
    }
}
