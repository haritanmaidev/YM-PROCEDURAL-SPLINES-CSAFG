using UnityEngine;

// This script will control a LineRenderer and
// later generate spline points for it.
[RequireComponent(typeof(LineRenderer))]
public class PGSSpline : MonoBehaviour
{
    [Header("Manual Mode")]
    public Transform[] controlPoints;

    [Header("Procedural Mode")]
    public Transform startPoint;
    public Transform endPoint;

    //[Range(-20f, 20f)] public float curvature = 0f;
    [Range(-20, 20f)] public float horizontalCurvature = 0f;
    [Range(-20, 20f)] public float verticalCurvature = 0f;
    [Range(2, 30)] public int splinePointCount = 4;
    [Range(1, 5)] public int splineSegmentResolution = 1;
    [Range(1, 3)] public float pathLengthMultiplier = 1f;

    public bool useProceduralPoints = true;
    public Vector3[] generatedPoints;

    private LineRenderer line;

//CHECK: is splinePointCount same as interalPointCount?
//TODO: Curvature now can determine only left and right curve arching. it should be able to go up and down as well
//TODO: Arc factor weights the curvature whose weight at A and B = 0, mid point = 1. Spreads the effect of curvature along the spline.
//Maybe expose where the peak weight should be, for more control
//CHECK: Should we clamp curvature based on distance between A and B?
//CONSIDER: Should I leave the pathLengthMultipier? I could just have the user place the start n end further.

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
    }

    private void Update()
    {
        if (useProceduralPoints)
        {
            GenerateControlPointsFromAtoB();
            DrawCatmullRomProceduralSpline(generatedPoints);
        }
        else
        {
            if (controlPoints != null && controlPoints.Length >= 4) DrawCatmullRomFromTransforms(controlPoints);
        }
    }

    private void GenerateControlPointsFromAtoB()
    {
        if (startPoint == null || endPoint == null)
            return;

        Vector3 A = startPoint.position;
        Vector3 B = endPoint.position;
        Vector3 AB = B - A;
        Vector3 dir = AB.normalized;
        float distance = AB.magnitude;

        //Build array of points: A/Start + internal/spline points + B/End
        int totalPointsLength = splinePointCount + 2;
        generatedPoints = new Vector3[totalPointsLength];

        float extra = distance * (pathLengthMultiplier - 1f) * 0.5f;
        Vector3 extendedA = A - dir * extra;
        Vector3 extendedB = B + dir * extra;
        float extendedDistance = (extendedB - extendedA).magnitude;

        generatedPoints[0] = A;
        generatedPoints[totalPointsLength - 1] = B;

        #region 4 manual point old code
        // Two base points along the straight line A -> B
        //Vector3 p1Base = A;
        //Vector3 p2Base = A + dir * (distance * 1.0f);
        //Vector3 p1Base = A + dir * (distance * 0.33f); // about 1/3 of the way
        //Vector3 p2Base = A + dir * (distance * 0.66f); // about 2/3 of the way
        #endregion
        Vector3 side = Vector3.Cross(dir, Vector3.up).normalized;  // Sideways direction (perpendicular to AB, using world up)

        //ASK: Why totalPoints - 2? Because end point is actually at [totalPoints - 1] in terms of indexing
        //Placement Logic: Looping through p1 to pN-2: Only the spline points and not the start/end points
        for (int p = 1; p <= totalPointsLength - 2; p++) 
        {
        //ASK: Why totalPoints - 1?
            float t = p / (float)(totalPointsLength - 1);
            Vector3 onLine = extendedA + dir * (extendedDistance * t);

            float arcFactor = Mathf.Sin(Mathf.PI * t); // Curvature offset; you can scale by distance if you want stronger bend on long lines
            Vector3 offset = (side * horizontalCurvature * arcFactor) + (Vector3.up * verticalCurvature * arcFactor);

            generatedPoints[p] = onLine + offset;
        }
    }

    private void DrawCatmullRomProceduralSpline(Vector3[] points)
    {
        //Catmull-Rom Segement = Total Points - 3 because a segement here denotes the curve between p1 and p2, p0 and p3 are used for tangents
//ASK: What does it mean that start n end are used for tangents?
        int totalCatmullCurves = points.Length - 3;
        int totalSegments = totalCatmullCurves * splineSegmentResolution;
 //ASK: WHY +1?
//FORUM: The +1 is necessary to include the last point of the spline in the LineRenderer.
        line.positionCount = totalSegments + 1;
//FORUM: Say how I had an issue where I didn't do +1 which lead to the last point not being drawn.
        //Storing positions of each per each curve segment 
        int index = 0;
        for (int i = 0; i < totalCatmullCurves; i++)
        {
            Vector3 p0 = points[i];
            Vector3 p1 = points[i + 1];
            Vector3 p2 = points[i + 2];
            Vector3 p3 = points[i + 3];

            //Evaluate t on each catmull's curve segment based on total segment/resolution count per each curve segment
            for (int s = 0; s < splineSegmentResolution; s++)
            {
                float t = s / (float)splineSegmentResolution;
                line.SetPosition(index, CatmullRom(p0, p1, p2, p3, t));
                index++;
            }
        }

        // last point (t=1)
        Vector3 lp0 = points[points.Length - 4];
        Vector3 lp1 = points[points.Length - 3];
        Vector3 lp2 = points[points.Length - 2];
        Vector3 lp3 = points[points.Length - 1];

        line.SetPosition(index, CatmullRom(lp0, lp1, lp2, lp3, 1f));
    }

    private void DrawCatmullRomFromTransforms(Transform[] tPoints) 
    {
        //Convert transform into position Vector3 and pass it to ProceduralSplineDrawer / DrawCatmullRomVector3Array
        Vector3[] points = new Vector3[tPoints.Length];
        for (int i = 0; i < tPoints.Length; i++)
        {
            points[i] = tPoints[i].position;
        }

        DrawCatmullRomProceduralSpline(points);
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
        Vector3[] pts = null;

        if (useProceduralPoints)
        {
            // In the editor, GenerateControlPointsFromAtoB can still run,
            // because it only uses Transform positions.
            if (startPoint == null || endPoint == null)
                return;

            GenerateControlPointsFromAtoB();
            pts = generatedPoints;
        }
        else
        {
            if (controlPoints == null || controlPoints.Length < 4)
                return;

            pts = new Vector3[controlPoints.Length];
            for (int i = 0; i < controlPoints.Length; i++)
            {
                if (controlPoints[i] != null)
                    pts[i] = controlPoints[i].position;
            }
        }

        if (pts == null || pts.Length < 4)
            return;
        
        //Draw spheres at control points
        Gizmos.color = Color.yellow;
        float sphereRadius = 0.065f;

        for (int i = 0; i < pts.Length; i++)
        {
            Gizmos.DrawSphere(pts[i], sphereRadius);
        }

        Gizmos.color = Color.red; 
        Gizmos.DrawSphere(pts[0], sphereRadius * 1.2f);   // p0
        Gizmos.color = Color.green; 
        Gizmos.DrawSphere(pts[1], sphereRadius * 1.2f);   // p1
        int lastP = pts.Length - 2;
        Gizmos.color = Color.green; 
        Gizmos.DrawSphere(pts[lastP], sphereRadius * 1.2f);   // pN - 2
        int endP = pts.Length - 1;
        Gizmos.color = Color.red; 
        Gizmos.DrawSphere(pts[endP], sphereRadius * 1.2f);   // pN - 1

        //Spline Color
        Gizmos.color = Color.white;
        int numCurves = pts.Length - 3;
        if (numCurves <= 0)
            return;

        Vector3 previousPos = CatmullRom(pts[0], pts[1], pts[2], pts[3], 0f);

        for (int i = 0; i < numCurves; i++)
        {
            Vector3 p0 = pts[i];
            Vector3 p1 = pts[i + 1];
            Vector3 p2 = pts[i + 2];
            Vector3 p3 = pts[i + 3];

            for (int s = 1; s <= splineSegmentResolution; s++)
            {
                float t = s / (float)splineSegmentResolution;
                Vector3 pos = CatmullRom(p0, p1, p2, p3, t);

                Gizmos.DrawLine(previousPos, pos);
                previousPos = pos;
            }
        }
    }
}
