using UnityEngine;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using motionportrait;

public class EditFPController : MonoBehaviour
{
    enum MESH_DATA_TYPE
    {
        MESH_DATA_TYPE_IMAGE = 0,
        MESH_DATA_TYPE_NUM,
    };

    enum FP_ELEMENT
    {
        FP_ELEMENT_EYE_LEFT = 0,
        FP_ELEMENT_EYE_RIGHT,
        FP_ELEMENT_MOUTH_LEFT,
        FP_ELEMENT_MOUTH_CENTER,
        FP_ELEMENT_MOUTH_RIGHT,
        FP_ELEMENT_FACE_LEFT,
        FP_ELEMENT_FACE_TOP,
        FP_ELEMENT_FACE_RIGHT,
        FP_ELEMENT_FACE_BOTTOM,
        FP_ELEMENT_NUM,
    };

    enum EDIT_STEP
    {
        EDIT_STEP_BEGINNING = 0,
        EDIT_STEP_FACE,
        EDIT_STEP_EYE,
        EDIT_STEP_MOUTH,
        EDIT_STEP_NUM,
    }

    public class NormalizedFeaturePoints
    {
        // normalized coordinates
        public Vector2[] EyeLeftPoints;
        public Vector2[] EyeRightPoints;
        public Vector2[] MouthPoints;
        public Vector2[] FacePoints;

        // rotation angles in units of degree
        public float EyeLeftAngleDegree;
        public float EyeRightAngleDegree;
    };

    // rectangle with the rotation for each eye
    class EyeRect
    {
        // center coordinates of the rectangle
        public Vector3 Point;

        // width and height of the rectangle
        public Vector2 Size;

        // rotation angle in units of degree
        public float AngleDegree;

        public EyeRect()
        {
            Point = Vector3.zero;
            Size = Vector2.zero;
            AngleDegree = 0.0f;
        }
    };

    // mesh objects
    private GameObject[] meshObj_;

    // camera object
    private Camera camera_;

    // texture for input image
    private Texture2D inputTexture_;

    // texture for painting
    private Texture2D paintTexture_;

    // feature points in screen coordinate system
    private EyeRect eyeLeftRect_;
    private EyeRect eyeRightRect_;
    private Vector2 eyeSizeStep_;
    private Vector3[] mouthPoints_;
    private Vector3[] facePoints_;

    // feature points in normalized coordinates on image mesh
    private NormalizedFeaturePoints normFP_;
    private NormalizedFeaturePoints normFPOnStart_;

    // flag of feature points editing mode
    private bool fpEditing_;

    // variables to store information about user actions
    private bool downContinuing_;
    private Vector2 curMousePos_;
    private FP_ELEMENT selectedElement_;

    // current step
    private EDIT_STEP curEditStep_;

    // constants for eye rotation
    private readonly float eyeRotateAngleDegreeStep = 5.0f;
    private readonly float eyeRotateAngleDegreeLowerLimit = -60.0f;
    private readonly float eyeRotateAngleDegreeUpperLimit = 60.0f;

    // ratio to extend the eye rectangle, relative to the distance between the eyes
    private readonly float eyeRectExtensionRatio = 0.03f;

    // ratio for the eye size step, relative to the distance between the eyes
    private readonly float eyeSizeStepRatio = 0.03f;

    // factor for the point size, relative to the distance between the eyes
    private readonly float pointSizeFactor = 0.20f;

    // upper limit for the point size, relative to the smaller displayed image dimension
    private readonly float pointSizeUpperLimitRatio = 0.03f;

    // lower limit for the point size, relative to the smaller displayed image dimension
    private readonly float pointSizeLowerLimitRatio = 0.01f;

    // factor for the highlighted point size, relative to the normal point size
    private readonly float highlightScaleFactor = 2.0f;

    // parameters for drawing elements
    private float pointSize_;
    private readonly Color eyeLineColorDefault = Color.cyan;
    private readonly Color eyeLineColorSelected = Color.magenta;
    private readonly Color eyeHighlightColor = new(Color.cyan.r, Color.cyan.g, Color.cyan.b, 0.50f);

#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
    private readonly float eyeLineWidth = 4.0f;
#else
    private readonly float eyeLineWidth = 1.0f;
#endif

    private readonly Color mouthPointColorDefault = Color.cyan;
    private readonly Color mouthPointColorSelected = Color.magenta;
    private readonly Color mouthPointHighlightColor = new(Color.cyan.r, Color.cyan.g, Color.cyan.b, 0.50f);
    private readonly Color mouthLineColor = Color.black;
    private readonly Color facePointColorDefault = Color.green;
    private readonly Color facePointColorSelected = Color.magenta;
    private readonly Color facePointHighlightColor = new(Color.green.r, Color.green.g, Color.green.b, 0.50f);
    private readonly Color faceLineColor = Color.green;
    private readonly Color faceLineColorSelected = Color.magenta;
    private readonly float faceLineWidthSelected = 4.0f;

    // default feature points
    private readonly Vector2[] fpEyeLeftDefault = new Vector2[]
    {
        new(0.25f, 0.60f),
        new(0.30f, 0.65f),
        new(0.35f, 0.60f),
        new(0.30f, 0.55f),
    };
    private readonly Vector2[] fpEyeRightDefault = new Vector2[]
    {
        new(0.65f, 0.60f),
        new(0.70f, 0.65f),
        new(0.75f, 0.60f),
        new(0.70f, 0.55f),
    };
    private readonly Vector2[] fpMouthDefault = new Vector2[]
    {
        new(0.40f, 0.30f),
        new(0.50f, 0.30f),
        new(0.60f, 0.30f),
    };
    private readonly Vector2[] fpFaceDefault = new Vector2[]
    {
        new(0.20f, 0.50f),
        new(0.50f, 0.80f),
        new(0.80f, 0.50f),
        new(0.50f, 0.20f),
    };

    void Start()
    {
        // mesh
        meshObj_ = new GameObject[(int)MESH_DATA_TYPE.MESH_DATA_TYPE_NUM];
        meshObj_[(int)MESH_DATA_TYPE.MESH_DATA_TYPE_IMAGE] = transform.Find("Image").gameObject;

        // get camera
        camera_ = GameObject.Find("Main Camera").GetComponent<Camera>();

        // initialize member variables
        fpEditing_ = false;
        inputTexture_ = null;
        Texture2D texture = new(1, 1);
        texture.Apply();
        paintTexture_ = texture;
        eyeLeftRect_ = new();
        eyeRightRect_ = new();
        eyeSizeStep_ = Vector2.zero;
        mouthPoints_ = new Vector3[3];
        for (int cnt = 0; cnt < mouthPoints_.Length; ++cnt)
        {
            mouthPoints_[cnt] = Vector3.zero;
        }
        facePoints_ = new Vector3[4];
        for (int cnt = 0; cnt < facePoints_.Length; ++cnt)
        {
            facePoints_[cnt] = Vector3.zero;
        }
        downContinuing_ = false;
        curMousePos_ = Vector2.zero;
        ClearSelectedElement();

        curEditStep_ = EDIT_STEP.EDIT_STEP_BEGINNING;

        // point size is initialized here and recalculated based on the distance
        // between the eyes once the feature points are available
        pointSize_ = 0.0f;
    }

    void ClearSelectedElement()
    {
        selectedElement_ = FP_ELEMENT.FP_ELEMENT_NUM;
    }

    Vector3 WorldToScreenPoint(Vector2 pos2d)
    {
        return camera_.WorldToScreenPoint(new Vector3(pos2d.x, -pos2d.y, 0.0f));
    }

    Vector3 ScreenToWorldPoint(Vector3 pos3d)
    {
        return camera_.ScreenToWorldPoint(pos3d);
    }

    Rect GetRect(Vector3 pos3d, float sizeX, float sizeY)
    {
        Rect rect = new(pos3d.x - sizeX / 2, pos3d.y - sizeY / 2, sizeX, sizeY);
        return rect;
    }

    Rect GetPixelRect(Vector3 pos3d, float sizeX, float sizeY)
    {
        float left = Mathf.Floor(pos3d.x - sizeX / 2);
        float right = Mathf.Ceil(pos3d.x + sizeX / 2);
        float top = Mathf.Floor(pos3d.y - sizeY / 2);
        float bottom = Mathf.Ceil(pos3d.y + sizeY / 2);
        return new Rect(left, top, right - left, bottom - top);
    }

    void DrawCircle(Vector3 pos3d, float size, Color color)
    {
        Rect rect = GetRect(pos3d, size, size);
        GUI.DrawTexture(rect, paintTexture_, ScaleMode.StretchToFill, false, 0, color, Vector4.zero, size / 2);
    }

    void DrawRect(Vector3 pos3d, float size, Color color)
    {
        Rect rect = GetRect(pos3d, size, size);
        GUI.DrawTexture(rect, paintTexture_, ScaleMode.StretchToFill, true, 0, color, 0, 0);
    }

    void Translate(Vector2 trans)
    {
        Matrix4x4 mat = Matrix4x4.identity;
        mat.SetRow(0, new(1, 0, 0, trans.x));
        mat.SetRow(1, new(0, 1, 0, trans.y));
        GUI.matrix *= mat;
    }

    void Rotate(float rotAngle)
    {
        float cs = Mathf.Cos(rotAngle);
        float sn = Mathf.Sin(rotAngle);
        Matrix4x4 mat = Matrix4x4.identity;
        mat.SetRow(0, new(cs, sn, 0, 0));
        mat.SetRow(1, new(-sn, cs, 0, 0));
        GUI.matrix *= mat;
    }

    void DrawEyeRect(EyeRect info, Color color, float lineWidth)
    {
        Vector3 pos3d = info.Point;
        Vector2 size = info.Size;
        float rotAngle = info.AngleDegree * Mathf.Deg2Rad;
        Rect rect = GetPixelRect(Vector3.zero, size.x, size.y);
        float borderRadius = Mathf.Min(rect.width, rect.height) / 2;
        Vector2 rotCenter = new(Mathf.Round(pos3d.x), Mathf.Round(pos3d.y));
        Translate(rotCenter);
        Rotate(rotAngle);
        GUI.DrawTexture(rect, paintTexture_, ScaleMode.StretchToFill, true, 0, color, lineWidth, borderRadius);
        Rotate(-rotAngle);
        Translate(-rotCenter);
    }

    void DrawLine(Collection<Vector3> points, Color color)
    {
        foreach (Vector3 p in points)
        {
            DrawRect(p, 1, color);
        }
    }

    void CreateLine(Vector3 start, Vector3 end, ref Collection<Vector3> oPoints)
    {
        Vector3Int c0 = new()
        {
            x = (int)start.x,
            y = (int)start.y,
        };
        Vector3Int c1 = new()
        {
            x = (int)end.x,
            y = (int)end.y,
        };

        Vector3Int dist = new()
        {
            x = Mathf.Abs(c1.x - c0.x),
            y = Mathf.Abs(c1.y - c0.y)
        };

        Vector3Int dir = new()
        {
            x = (c1.x > c0.x) ? 1 : -1,
            y = (c1.y > c0.y) ? 1 : -1
        };

        Collection<Vector3Int> pixels = new();
        if (dist.x > dist.y)
        {
            int e = -dist.x;
            for (int cnt = 0; cnt <= dist.x; ++cnt)
            {
                pixels.Add(c0);
                c0.x += dir.x;
                e += 2 * dist.y;
                if (e >= 0)
                {
                    c0.y += dir.y;
                    e -= 2 * dist.x;
                }
            }
        }
        else
        {
            int e = -dist.y;
            for (int cnt = 0; cnt <= dist.y; ++cnt)
            {
                pixels.Add(c0);
                c0.y += dir.y;
                e += 2 * dist.x;
                if (e >= 0)
                {
                    c0.x += dir.x;
                    e -= 2 * dist.y;
                }
            }
        }

        Collection<Vector3> points = new();
        foreach (Vector3Int p in pixels)
        {
            Vector3 v = new();
            {
                v.x = p.x; v.y = p.y; v.z = p.z;
            }
            points.Add(v);
        }

        // output
        oPoints = points;
    }

    void OnGUI()
    {
        if (fpEditing_)
        {
            if (curEditStep_ == EDIT_STEP.EDIT_STEP_NUM)
            {
                return;
            }
            Color clearColor = new(0, 0, 0, 0);
            float highlightSize = highlightScaleFactor * pointSize_;
            Color eyeLeftLineColor = eyeLineColorDefault;
            Color eyeLeftHighlightColor = clearColor;
            Color eyeRightLineColor = eyeLineColorDefault;
            Color eyeRightHighlightColor = clearColor;
            Color[] mouthPointColorArray = new Color[3];
            Color[] mouthPointHighlightColorArray = new Color[3];
            Color[] facePointColorArray = new Color[4];
            Color[] facePointHighlightColorArray = new Color[4];
            bool[] faceRectSideSeleted = new bool[4];
            for (int cnt = 0; cnt < mouthPointColorArray.Length; ++cnt)
            {
                mouthPointColorArray[cnt] = mouthPointColorDefault;
                mouthPointHighlightColorArray[cnt] = clearColor;
            }
            for (int cnt = 0; cnt < facePointColorArray.Length; ++cnt)
            {
                facePointColorArray[cnt] = facePointColorDefault;
                facePointHighlightColorArray[cnt] = clearColor;
            }
            switch (selectedElement_)
            {
                case FP_ELEMENT.FP_ELEMENT_EYE_LEFT:
                    eyeLeftLineColor = eyeLineColorSelected;
                    eyeLeftHighlightColor = downContinuing_ ? eyeHighlightColor : clearColor;
                    break;
                case FP_ELEMENT.FP_ELEMENT_EYE_RIGHT:
                    eyeRightLineColor = eyeLineColorSelected;
                    eyeRightHighlightColor = downContinuing_ ? eyeHighlightColor : clearColor;
                    break;
                case FP_ELEMENT.FP_ELEMENT_MOUTH_LEFT:
                    mouthPointColorArray[0] = mouthPointColorSelected;
                    mouthPointHighlightColorArray[0] = mouthPointHighlightColor;
                    break;
                case FP_ELEMENT.FP_ELEMENT_MOUTH_CENTER:
                    mouthPointColorArray[1] = mouthPointColorSelected;
                    mouthPointHighlightColorArray[1] = mouthPointHighlightColor;
                    break;
                case FP_ELEMENT.FP_ELEMENT_MOUTH_RIGHT:
                    mouthPointColorArray[2] = mouthPointColorSelected;
                    mouthPointHighlightColorArray[2] = mouthPointHighlightColor;
                    break;
                case FP_ELEMENT.FP_ELEMENT_FACE_LEFT:
                    facePointColorArray[0] = facePointColorSelected;
                    facePointHighlightColorArray[0] = facePointHighlightColor;
                    faceRectSideSeleted[0] = true;
                    break;
                case FP_ELEMENT.FP_ELEMENT_FACE_TOP:
                    facePointColorArray[1] = facePointColorSelected;
                    facePointHighlightColorArray[1] = facePointHighlightColor;
                    faceRectSideSeleted[1] = true;
                    break;
                case FP_ELEMENT.FP_ELEMENT_FACE_RIGHT:
                    facePointColorArray[2] = facePointColorSelected;
                    facePointHighlightColorArray[2] = facePointHighlightColor;
                    faceRectSideSeleted[2] = true;
                    break;
                case FP_ELEMENT.FP_ELEMENT_FACE_BOTTOM:
                    facePointColorArray[3] = facePointColorSelected;
                    facePointHighlightColorArray[3] = facePointHighlightColor;
                    faceRectSideSeleted[3] = true;
                    break;
            }

            if (curEditStep_ == EDIT_STEP.EDIT_STEP_EYE || curEditStep_ == EDIT_STEP.EDIT_STEP_BEGINNING)
            {
                DrawEyeRect(eyeLeftRect_, eyeLeftLineColor, eyeLineWidth);
                DrawCircle(eyeLeftRect_.Point, highlightSize, eyeLeftHighlightColor);
                DrawEyeRect(eyeRightRect_, eyeRightLineColor, eyeLineWidth);
                DrawCircle(eyeRightRect_.Point, highlightSize, eyeRightHighlightColor);
            }

            if (curEditStep_ == EDIT_STEP.EDIT_STEP_MOUTH || curEditStep_ == EDIT_STEP.EDIT_STEP_BEGINNING)
            {
                for (int cnt = 0; cnt < mouthPoints_.Length - 1; ++cnt)
                {
                    Collection<Vector3> points = new();
                    CreateLine(mouthPoints_[cnt], mouthPoints_[cnt + 1], ref points);
                    DrawLine(points, mouthLineColor);
                }
                for (int cnt = 0; cnt < mouthPoints_.Length; ++cnt)
                {
                    DrawCircle(mouthPoints_[cnt], highlightSize, mouthPointHighlightColorArray[cnt]);
                }
                for (int cnt = 0; cnt < mouthPoints_.Length; ++cnt)
                {
                    DrawCircle(mouthPoints_[cnt], pointSize_, mouthPointColorArray[cnt]);
                }
            }

            if (curEditStep_ == EDIT_STEP.EDIT_STEP_FACE || curEditStep_ == EDIT_STEP.EDIT_STEP_BEGINNING)
            {
                Vector3 faceLeftPos = facePoints_[0];
                Vector3 faceTopPos = facePoints_[1];
                Vector3 faceRightPos = facePoints_[2];
                Vector3 faceBottomPos = facePoints_[3];
                Vector3 faceCenterPos = new((faceLeftPos.x + faceRightPos.x) / 2, (faceBottomPos.y + faceTopPos.y) / 2, 0.0f);
                float faceRectWidth = faceRightPos.x - faceLeftPos.x;
                float faceRectHeight = faceBottomPos.y - faceTopPos.y;
                Rect faceRect = GetPixelRect(faceCenterPos, faceRectWidth, faceRectHeight);
                GUI.DrawTexture(faceRect, paintTexture_, ScaleMode.StretchToFill, true, 0, faceLineColor, 1.0f, 0);

                if (faceRectSideSeleted[0])
                {
                    Rect rect = new(faceLeftPos.x - faceLineWidthSelected, faceTopPos.y, faceLineWidthSelected, faceRectHeight);
                    GUI.DrawTexture(rect, paintTexture_, ScaleMode.StretchToFill, false, 0, faceLineColorSelected / 2, 0, 0);
                }
                else if (faceRectSideSeleted[1])
                {
                    Rect rect = new(faceLeftPos.x, faceTopPos.y - faceLineWidthSelected, faceRectWidth, faceLineWidthSelected);
                    GUI.DrawTexture(rect, paintTexture_, ScaleMode.StretchToFill, false, 0, faceLineColorSelected / 2, 0, 0);
                }
                else if (faceRectSideSeleted[2])
                {
                    Rect rect = new(faceRightPos.x, faceTopPos.y, faceLineWidthSelected, faceRectHeight);
                    GUI.DrawTexture(rect, paintTexture_, ScaleMode.StretchToFill, false, 0, faceLineColorSelected / 2, 0, 0);
                }
                else if (faceRectSideSeleted[3])
                {
                    Rect rect = new(faceLeftPos.x, faceBottomPos.y, faceRectWidth, faceLineWidthSelected);
                    GUI.DrawTexture(rect, paintTexture_, ScaleMode.StretchToFill, false, 0, faceLineColorSelected / 2, 0, 0);
                }

                for (int cnt = 0; cnt < facePoints_.Length; ++cnt)
                {
                    DrawCircle(facePoints_[cnt], highlightSize, facePointHighlightColorArray[cnt]);
                }
                for (int cnt = 0; cnt < facePoints_.Length; ++cnt)
                {
                    DrawCircle(facePoints_[cnt], pointSize_, facePointColorArray[cnt]);
                }
            }
        }
    }

    FP_ELEMENT FindElement(Vector2 mousePos)
    {
        Dictionary<FP_ELEMENT, Vector2> dict;
        switch (curEditStep_)
        {
            case EDIT_STEP.EDIT_STEP_EYE:
                dict = new()
                {
                    [FP_ELEMENT.FP_ELEMENT_EYE_LEFT] = eyeLeftRect_.Point,
                    [FP_ELEMENT.FP_ELEMENT_EYE_RIGHT] = eyeRightRect_.Point,
                };
                break;
            case EDIT_STEP.EDIT_STEP_MOUTH:
                dict = new()
                {
                    [FP_ELEMENT.FP_ELEMENT_MOUTH_LEFT] = mouthPoints_[0],
                    [FP_ELEMENT.FP_ELEMENT_MOUTH_CENTER] = mouthPoints_[1],
                    [FP_ELEMENT.FP_ELEMENT_MOUTH_RIGHT] = mouthPoints_[2],
                };
                break;
            case EDIT_STEP.EDIT_STEP_FACE:
                dict = new()
                {
                    [FP_ELEMENT.FP_ELEMENT_FACE_LEFT] = facePoints_[0],
                    [FP_ELEMENT.FP_ELEMENT_FACE_TOP] = facePoints_[1],
                    [FP_ELEMENT.FP_ELEMENT_FACE_RIGHT] = facePoints_[2],
                    [FP_ELEMENT.FP_ELEMENT_FACE_BOTTOM] = facePoints_[3]
                };
                break;
            default:
                return FP_ELEMENT.FP_ELEMENT_NUM;
        }

        float x = mousePos.x;
        float y = Screen.height - mousePos.y;
        float minDist = float.MaxValue;
        FP_ELEMENT minDistElement = FP_ELEMENT.FP_ELEMENT_NUM;
        foreach (KeyValuePair<FP_ELEMENT, Vector2> kvp in dict)
        {
            FP_ELEMENT el = kvp.Key;
            Vector2 elPos = kvp.Value;
            float dx = x - elPos.x;
            float dy = y - elPos.y;
            float distSquared = dx * dx + dy * dy;
            if (distSquared < minDist)
            {
                minDist = distSquared;
                minDistElement = el;
            }
        }

        return minDistElement;
    }

    void TranslatePosition(ref Vector3 pos, Vector2 diff)
    {
        pos.x += diff.x;
        pos.y -= diff.y;
    }

    public void UpdateEditFP(bool down, bool up, Vector2 mousePos)
    {
        if (down)
        {
            FP_ELEMENT el = FindElement(mousePos);
            switch (el)
            {
                case FP_ELEMENT.FP_ELEMENT_EYE_LEFT:
                case FP_ELEMENT.FP_ELEMENT_EYE_RIGHT:
                case FP_ELEMENT.FP_ELEMENT_MOUTH_LEFT:
                case FP_ELEMENT.FP_ELEMENT_MOUTH_CENTER:
                case FP_ELEMENT.FP_ELEMENT_MOUTH_RIGHT:
                case FP_ELEMENT.FP_ELEMENT_FACE_LEFT:
                case FP_ELEMENT.FP_ELEMENT_FACE_TOP:
                case FP_ELEMENT.FP_ELEMENT_FACE_RIGHT:
                case FP_ELEMENT.FP_ELEMENT_FACE_BOTTOM:
                    // select an element
                    selectedElement_ = el;
                    curMousePos_ = mousePos;
                    break;
                default:
                    // clear selected element
                    ClearSelectedElement();
                    curMousePos_ = Vector2.zero;
                    break;
            }
            downContinuing_ = true;
        }
        else if (up)
        {
            switch (selectedElement_)
            {
                case FP_ELEMENT.FP_ELEMENT_MOUTH_LEFT:
                case FP_ELEMENT.FP_ELEMENT_MOUTH_CENTER:
                case FP_ELEMENT.FP_ELEMENT_MOUTH_RIGHT:
                case FP_ELEMENT.FP_ELEMENT_FACE_LEFT:
                case FP_ELEMENT.FP_ELEMENT_FACE_TOP:
                case FP_ELEMENT.FP_ELEMENT_FACE_RIGHT:
                case FP_ELEMENT.FP_ELEMENT_FACE_BOTTOM:
                    // clear selected element for mouth points
                    ClearSelectedElement();
                    curMousePos_ = Vector2.zero;
                    break;
            }
            downContinuing_ = false;
        }
        else if (downContinuing_)   // drag
        {
            // translate selected element
            Vector2 diff = mousePos - curMousePos_;
            switch (selectedElement_)
            {
                case FP_ELEMENT.FP_ELEMENT_EYE_LEFT:
                    TranslatePosition(ref eyeLeftRect_.Point, diff);
                    break;
                case FP_ELEMENT.FP_ELEMENT_EYE_RIGHT:
                    TranslatePosition(ref eyeRightRect_.Point, diff);
                    break;
                case FP_ELEMENT.FP_ELEMENT_MOUTH_LEFT:
                    TranslatePosition(ref mouthPoints_[0], diff);
                    break;
                case FP_ELEMENT.FP_ELEMENT_MOUTH_CENTER:
                    TranslatePosition(ref mouthPoints_[1], diff);
                    break;
                case FP_ELEMENT.FP_ELEMENT_MOUTH_RIGHT:
                    TranslatePosition(ref mouthPoints_[2], diff);
                    break;
                case FP_ELEMENT.FP_ELEMENT_FACE_LEFT:
                    TranslatePosition(ref facePoints_[0], diff);
                    break;
                case FP_ELEMENT.FP_ELEMENT_FACE_TOP:
                    TranslatePosition(ref facePoints_[1], diff);
                    break;
                case FP_ELEMENT.FP_ELEMENT_FACE_RIGHT:
                    TranslatePosition(ref facePoints_[2], diff);
                    break;
                case FP_ELEMENT.FP_ELEMENT_FACE_BOTTOM:
                    TranslatePosition(ref facePoints_[3], diff);
                    break;
            }

            // restrict the face rectangle so that it does not turn inside out
            if (facePoints_[0].x > facePoints_[2].x) facePoints_[0].x = facePoints_[2].x;
            if (facePoints_[0].y < facePoints_[1].y) facePoints_[0].y = facePoints_[1].y;
            if (facePoints_[0].y > facePoints_[3].y) facePoints_[0].y = facePoints_[3].y;
            if (facePoints_[1].x < facePoints_[0].x) facePoints_[1].x = facePoints_[0].x;
            if (facePoints_[1].x > facePoints_[2].x) facePoints_[1].x = facePoints_[2].x;
            if (facePoints_[1].y > facePoints_[3].y) facePoints_[1].y = facePoints_[3].y;
            if (facePoints_[2].x < facePoints_[0].x) facePoints_[2].x = facePoints_[0].x;
            if (facePoints_[2].y < facePoints_[1].y) facePoints_[2].y = facePoints_[1].y;
            if (facePoints_[2].y > facePoints_[3].y) facePoints_[2].y = facePoints_[3].y;
            if (facePoints_[3].x < facePoints_[0].x) facePoints_[3].x = facePoints_[0].x;
            if (facePoints_[3].x > facePoints_[2].x) facePoints_[3].x = facePoints_[2].x;
            if (facePoints_[3].y < facePoints_[0].y) facePoints_[3].y = facePoints_[0].y;

            // update normalized feature points
            SyncNormalizedFPWithScreenFP();

            // update current position
            curMousePos_ = mousePos;
        }
    }

    void Update()
    {
        // show/hide the mesh
        meshObj_[(int)MESH_DATA_TYPE.MESH_DATA_TYPE_IMAGE].SetActive(fpEditing_);

        if (fpEditing_)
        {
            // update input image texture
            Renderer[] bgRenderer = meshObj_[(int)MESH_DATA_TYPE.MESH_DATA_TYPE_IMAGE].GetComponentsInChildren<Renderer>();
            for (int i = 0; i < bgRenderer.Length; i++)
            {
                bgRenderer[i].material.mainTexture = inputTexture_;
                bgRenderer[i].material.color = Color.white;
            }
        }
    }

    int GetFeaturePoint(MpaAnalyzer.MpaRecogResult recogResult, MpaAnalyzer.eFeaturePoint key, ref Vector2 oPos)
    {
        bool available = false;
        MpTypes.mpVector2 pos = new();
        int ret = recogResult.GetFeaturePoint(key, ref pos.x, ref pos.y, ref available);
        if (ret == 0 && available)
        {
            oPos.x = pos.x;
            oPos.y = pos.y;
            return 0;
        }
        return -1;
    }

    Vector2 CalcPos(Vector2 fp, Bounds meshBounds)
    {
        return new Vector2()
        {
            x = meshBounds.min.x + fp.x * meshBounds.size.x,
            y = meshBounds.min.y + fp.y * meshBounds.size.y,
        };
    }

    // calculate a rectangle with rotation
    void CalcEyeRectWithRotation(
        Vector2[] fpPoints, int imageWidth, int imageHeight, float eyeDistance,
        ref Vector2[] fpRectPoints, ref float rectAngleDegree)
    {
        // get coordinates in image scale
        Vector2[] points = new Vector2[4];
        for (int cnt = 0; cnt < 4; ++cnt)
        {
            Vector2 fp = fpPoints[cnt];
            Vector2 pos = new()
            {
                x = fp.x * imageWidth,
                y = fp.y * imageHeight,
            };
            points[cnt] = pos;
        }

        // calculate the angle
        Vector2 leftPos = points[0];
        Vector2 rightPos = points[2];
        Vector2 eyeLeftToRightVec = rightPos - leftPos;
        eyeLeftToRightVec.Normalize();
        float angle = Mathf.Asin(eyeLeftToRightVec.y);
        float cs = Mathf.Cos(angle);
        float sn = Mathf.Sin(angle);

        // calculate the center point
        Vector2 centerPos = (leftPos + rightPos) / 2;

        // rotate points inversely
        Vector2[] rotPoints = new Vector2[4];
        for (int cnt = 0; cnt < 4; ++cnt)
        {
            Vector2 pos = points[cnt];
            Vector2 relPos = pos - centerPos;
            Vector2 rotRelPos = new()
            {
                x = cs * relPos.x + sn * relPos.y,
                y = -sn * relPos.x + cs * relPos.y,
            };
            rotPoints[cnt] = rotRelPos + centerPos;
        }
        Vector2 rotLeftPos = rotPoints[0];
        Vector2 rotTopPos = rotPoints[1];
        Vector2 rotRightPos = rotPoints[2];
        Vector2 rotBottomPos = rotPoints[3];

        // extend the rectangle based on the distance between the eyes
        float rectExtension = eyeRectExtensionRatio * eyeDistance;
        float rectWidthExtension = rectExtension;
        float rectHeightExtension = rectExtension;
        float rectHalfWidth = (rotRightPos.x - rotLeftPos.x) / 2 + rectWidthExtension;
        float rectHalfHeight = (rotTopPos.y - rotBottomPos.y) / 2 + rectHeightExtension;
        Vector2 rectLeftPos = new()
        {
            x = centerPos.x - rectHalfWidth,
            y = centerPos.y,
        };
        Vector2 rectTopPos = new()
        {
            x = centerPos.x,
            y = centerPos.y + rectHalfHeight,
        };
        Vector2 rectRightPos = new()
        {
            x = centerPos.x + rectHalfWidth,
            y = centerPos.y,
        };
        Vector2 rectBottomPos = new()
        {
            x = centerPos.x,
            y = centerPos.y - rectHalfHeight,
        };
        Vector2[] rectPoints = { rectLeftPos, rectTopPos, rectRightPos, rectBottomPos };

        // output normalized points
        for (int cnt = 0; cnt < 4; ++cnt)
        {
            Vector2 pos = rectPoints[cnt];
            Vector2 fp = new()
            {
                x = pos.x / imageWidth,
                y = pos.y / imageHeight,
            };
            fpRectPoints[cnt] = fp;
        }

        // output angle
        float angleDegree = angle * Mathf.Rad2Deg;
        if (angleDegree < eyeRotateAngleDegreeLowerLimit)
        {
            angleDegree = eyeRotateAngleDegreeLowerLimit;
        }
        else if (angleDegree > eyeRotateAngleDegreeUpperLimit)
        {
            angleDegree = eyeRotateAngleDegreeUpperLimit;
        }
        int angleCnt = (int)(angleDegree / eyeRotateAngleDegreeStep);
        rectAngleDegree = angleCnt * eyeRotateAngleDegreeStep;
    }

    void UpdateNormalizedFeaturePoints(MpaAnalyzer.MpaRecogResult recogResult, int imageWidth, int imageHeight)
    {
        int ret;

        Vector2 fpMouthLeft = new();
        ret = GetFeaturePoint(recogResult, MpaAnalyzer.eFeaturePoint.FP_MOUTH_LEFT, ref fpMouthLeft);
        if (ret != 0)
        {
            fpMouthLeft = fpMouthDefault[0];
        }
        Vector2 fpMouthCenter = new();
        ret = GetFeaturePoint(recogResult, MpaAnalyzer.eFeaturePoint.FP_BASICPARTS_MOUTH, ref fpMouthCenter);
        if (ret != 0)
        {
            fpMouthCenter = fpMouthDefault[1];
        }
        Vector2 fpMouthRight = new();
        ret = GetFeaturePoint(recogResult, MpaAnalyzer.eFeaturePoint.FP_MOUTH_RIGHT, ref fpMouthRight);
        if (ret != 0)
        {
            fpMouthRight = fpMouthDefault[2];
        }
        Vector2[] fpMouthPoints = { fpMouthLeft, fpMouthCenter, fpMouthRight };

        Vector2 fpEyeLeftTop = new();
        ret = GetFeaturePoint(recogResult, MpaAnalyzer.eFeaturePoint.FP_EYE_LEFT_TOP, ref fpEyeLeftTop);
        if (ret != 0)
        {
            fpEyeLeftTop = fpEyeLeftDefault[0];
        }
        Vector2 fpEyeLeftBottom = new();
        ret = GetFeaturePoint(recogResult, MpaAnalyzer.eFeaturePoint.FP_EYE_LEFT_BOTTOM, ref fpEyeLeftBottom);
        if (ret != 0)
        {
            fpEyeLeftBottom = fpEyeLeftDefault[1];
        }
        Vector2 fpEyeLeftOutside = new();
        ret = GetFeaturePoint(recogResult, MpaAnalyzer.eFeaturePoint.FP_EYE_LEFT_OUTSIDE, ref fpEyeLeftOutside);
        if (ret != 0)
        {
            fpEyeLeftOutside = fpEyeLeftDefault[2];
        }
        Vector2 fpEyeLeftInside = new();
        ret = GetFeaturePoint(recogResult, MpaAnalyzer.eFeaturePoint.FP_EYE_LEFT_INSIDE, ref fpEyeLeftInside);
        if (ret != 0)
        {
            fpEyeLeftInside = fpEyeLeftDefault[3];
        }

        Vector2 fpEyeRightTop = new();
        ret = GetFeaturePoint(recogResult, MpaAnalyzer.eFeaturePoint.FP_EYE_RIGHT_TOP, ref fpEyeRightTop);
        if (ret != 0)
        {
            fpEyeRightTop = fpEyeRightDefault[0];
        }
        Vector2 fpEyeRightBottom = new();
        ret = GetFeaturePoint(recogResult, MpaAnalyzer.eFeaturePoint.FP_EYE_RIGHT_BOTTOM, ref fpEyeRightBottom);
        if (ret != 0)
        {
            fpEyeRightBottom = fpEyeRightDefault[1];
        }
        Vector2 fpEyeRightOutside = new();
        ret = GetFeaturePoint(recogResult, MpaAnalyzer.eFeaturePoint.FP_EYE_RIGHT_OUTSIDE, ref fpEyeRightOutside);
        if (ret != 0)
        {
            fpEyeRightOutside = fpEyeRightDefault[2];
        }
        Vector2 fpEyeRightInside = new();
        ret = GetFeaturePoint(recogResult, MpaAnalyzer.eFeaturePoint.FP_EYE_RIGHT_INSIDE, ref fpEyeRightInside);
        if (ret != 0)
        {
            fpEyeRightInside = fpEyeRightDefault[3];
        }

        Vector2 fpFaceLeft = new();
        ret = GetFeaturePoint(recogResult, MpaAnalyzer.eFeaturePoint.FP_BASICFACE_LEFT, ref fpFaceLeft);
        if (ret != 0)
        {
            fpFaceLeft = fpFaceDefault[0];
        }
        Vector2 fpFaceTop = new();
        ret = GetFeaturePoint(recogResult, MpaAnalyzer.eFeaturePoint.FP_BASICFACE_TOP, ref fpFaceTop);
        if (ret != 0)
        {
            fpFaceTop = fpFaceDefault[1];
        }
        Vector2 fpFaceRight = new();
        ret = GetFeaturePoint(recogResult, MpaAnalyzer.eFeaturePoint.FP_BASICFACE_RIGHT, ref fpFaceRight);
        if (ret != 0)
        {
            fpFaceRight = fpFaceDefault[2];
        }
        Vector2 fpFaceBottom = new();
        ret = GetFeaturePoint(recogResult, MpaAnalyzer.eFeaturePoint.FP_BASICFACE_BOTTOM, ref fpFaceBottom);
        if (ret != 0)
        {
            fpFaceBottom = fpFaceDefault[3];
        }

        // sort the points clockwise
        Vector2[] fpFacePoints = { fpFaceLeft, fpFaceTop, fpFaceRight, fpFaceBottom };
        Vector2[] fpEyeLeftPoints = { fpEyeLeftOutside, fpEyeLeftTop, fpEyeLeftInside, fpEyeLeftBottom };
        Vector2[] fpEyeRightPoints = { fpEyeRightInside, fpEyeRightTop, fpEyeRightOutside, fpEyeRightBottom };

        // calculate the distance between the eyes in image scale
        // (use the midpoint of the outside/inside corners as each eye center,
        //  consistent with the center used in CalcEyeRectWithRotation)
        Vector2 eyeLeftCenter = new()
        {
            x = (fpEyeLeftPoints[0].x + fpEyeLeftPoints[2].x) / 2 * imageWidth,
            y = (fpEyeLeftPoints[0].y + fpEyeLeftPoints[2].y) / 2 * imageHeight,
        };
        Vector2 eyeRightCenter = new()
        {
            x = (fpEyeRightPoints[0].x + fpEyeRightPoints[2].x) / 2 * imageWidth,
            y = (fpEyeRightPoints[0].y + fpEyeRightPoints[2].y) / 2 * imageHeight,
        };
        float eyeDistance = Vector2.Distance(eyeLeftCenter, eyeRightCenter);

        // calculate a rectangle with rotation for each eye
        Vector2[] fpEyeLeftRectPoints = new Vector2[4];
        float eyeLeftAngleDegree = 0.0f;
        CalcEyeRectWithRotation(fpEyeLeftPoints, imageWidth, imageHeight, eyeDistance, ref fpEyeLeftRectPoints, ref eyeLeftAngleDegree);
        Vector2[] fpEyeRightRectPoints = new Vector2[4];
        float eyeRightAngleDegree = 0.0f;
        CalcEyeRectWithRotation(fpEyeRightPoints, imageWidth, imageHeight, eyeDistance, ref fpEyeRightRectPoints, ref eyeRightAngleDegree);

        // update normalized feature points
        normFP_ = new NormalizedFeaturePoints()
        {
            MouthPoints = fpMouthPoints,
            FacePoints = fpFacePoints,
            EyeLeftPoints = fpEyeLeftRectPoints,
            EyeRightPoints = fpEyeRightRectPoints,
            EyeLeftAngleDegree = eyeLeftAngleDegree,
            EyeRightAngleDegree = eyeRightAngleDegree,
        };
    }

    // synchronize screen feature points with normalized feature points
    void SyncScreenFeaturePoints()
    {
        if (normFP_ == null)
        {
            return;
        }

        Bounds meshBounds = GetImageMeshBounds();

        // convert feature points to world points
        Vector2[] mouthPoints2d = new Vector2[3];
        for (int cnt = 0; cnt < 3; ++cnt)
        {
            mouthPoints2d[cnt] = CalcPos(normFP_.MouthPoints[cnt], meshBounds);
        }
        Vector2[] facePoints2d = new Vector2[4];
        for (int cnt = 0; cnt < 4; ++cnt)
        {
            facePoints2d[cnt] = CalcPos(normFP_.FacePoints[cnt], meshBounds);
        }
        Vector2[] eyeLeftBoundaryPoints2d = new Vector2[4];
        for (int cnt = 0; cnt < 4; ++cnt)
        {
            eyeLeftBoundaryPoints2d[cnt] = CalcPos(normFP_.EyeLeftPoints[cnt], meshBounds);
        }
        Vector2[] eyeRightBoundaryPoints2d = new Vector2[4];
        for (int cnt = 0; cnt < 4; ++cnt)
        {
            eyeRightBoundaryPoints2d[cnt] = CalcPos(normFP_.EyeRightPoints[cnt], meshBounds);
        }

        // convert world points to screen points
        Vector3[] mouthPoints = new Vector3[3];
        for (int cnt = 0; cnt < mouthPoints2d.Length; ++cnt)
        {
            mouthPoints[cnt] = WorldToScreenPoint(mouthPoints2d[cnt]);
        }
        Vector3[] facePoints = new Vector3[4];
        for (int cnt = 0; cnt < facePoints2d.Length; ++cnt)
        {
            facePoints[cnt] = WorldToScreenPoint(facePoints2d[cnt]);
        }
        Vector3[] eyeLeftBoundaryPoints = new Vector3[4];
        for (int cnt = 0; cnt < eyeLeftBoundaryPoints2d.Length; ++cnt)
        {
            eyeLeftBoundaryPoints[cnt] = WorldToScreenPoint(eyeLeftBoundaryPoints2d[cnt]);
        }
        Vector3 eyeLeftPoint = new()
        {
            x = (eyeLeftBoundaryPoints[0].x + eyeLeftBoundaryPoints[2].x) / 2,
            y = (eyeLeftBoundaryPoints[1].y + eyeLeftBoundaryPoints[3].y) / 2,
        };
        Vector2 eyeLeftSize = new()
        {
            x = Mathf.Abs(eyeLeftBoundaryPoints[2].x - eyeLeftBoundaryPoints[0].x),
            y = Mathf.Abs(eyeLeftBoundaryPoints[1].y - eyeLeftBoundaryPoints[3].y),
        };
        Vector3[] eyeRightBoundaryPoints = new Vector3[4];
        for (int cnt = 0; cnt < eyeRightBoundaryPoints2d.Length; ++cnt)
        {
            eyeRightBoundaryPoints[cnt] = WorldToScreenPoint(eyeRightBoundaryPoints2d[cnt]);
        }
        Vector3 eyeRightPoint = new()
        {
            x = (eyeRightBoundaryPoints[0].x + eyeRightBoundaryPoints[2].x) / 2,
            y = (eyeRightBoundaryPoints[1].y + eyeRightBoundaryPoints[3].y) / 2,
        };
        Vector2 eyeRightSize = new()
        {
            x = Mathf.Abs(eyeRightBoundaryPoints[2].x - eyeRightBoundaryPoints[0].x),
            y = Mathf.Abs(eyeRightBoundaryPoints[1].y - eyeRightBoundaryPoints[3].y),
        };

        // update member variables
        eyeLeftRect_ = new()
        {
            Point = eyeLeftPoint,
            Size = eyeLeftSize,
            AngleDegree = normFP_.EyeLeftAngleDegree,
        };
        eyeRightRect_ = new()
        {
            Point = eyeRightPoint,
            Size = eyeRightSize,
            AngleDegree = normFP_.EyeRightAngleDegree,
        };
        mouthPoints_ = mouthPoints;
        facePoints_ = facePoints;

        // update sizes of screen elements based on the distance between the eyes
        UpdateDrawingElementSizes();
    }

    // calculate the screen-coordinate center of an eye from its normalized rect points
    Vector3 GetEyeCenterScreenPoint(Vector2[] eyePoints, Bounds meshBounds)
    {
        Vector2 centerNorm = (eyePoints[0] + eyePoints[2]) / 2;
        Vector2 worldPos = CalcPos(centerNorm, meshBounds);
        return WorldToScreenPoint(worldPos);
    }

    // update the eye size step based on the distance between the eyes
    void UpdateDrawingElementSizes()
    {
        if (normFPOnStart_ == null)
        {
            return;
        }

        // distance between the eyes in screen coordinates, derived from the eye
        // feature points detected by the Analyzer (or the default rectangle
        // positions when the detection failed). these are the feature points at
        // the start of editing, so the sizes are not affected by user edits.
        Bounds meshBounds = GetImageMeshBounds();
        Vector3 eyeLeftPoint = GetEyeCenterScreenPoint(normFPOnStart_.EyeLeftPoints, meshBounds);
        Vector3 eyeRightPoint = GetEyeCenterScreenPoint(normFPOnStart_.EyeRightPoints, meshBounds);
        float eyeDistance = Vector2.Distance(eyeLeftPoint, eyeRightPoint);
        float step = eyeSizeStepRatio * eyeDistance;
        eyeSizeStep_ = new(step, step);

        // point size is based on the distance between the eyes
        pointSize_ = pointSizeFactor * eyeDistance;

        // clamp the point size so that it does not become too large or too small,
        // limiting it to a fraction of the smaller displayed image dimension.
        // the displayed image size is derived from the mesh bounds converted to
        // screen coordinates (consistent with ScreenToFeatureSize).
        Vector3 screenMinPos = WorldToScreenPoint(meshBounds.min);
        Vector3 screenMaxPos = WorldToScreenPoint(meshBounds.max);
        float displayedImageWidth = Mathf.Abs(screenMaxPos.x - screenMinPos.x);
        float displayedImageHeight = Mathf.Abs(screenMaxPos.y - screenMinPos.y);
        float displayedImageMinSize = Mathf.Min(displayedImageWidth, displayedImageHeight);
        float pointSizeUpperLimit = pointSizeUpperLimitRatio * displayedImageMinSize;
        float pointSizeLowerLimit = pointSizeLowerLimitRatio * displayedImageMinSize;
        pointSize_ = Mathf.Clamp(pointSize_, pointSizeLowerLimit, pointSizeUpperLimit);
    }

    Vector2 ConvertScreen3dToNormalized2d(Vector3 screenPos3d)
    {
        Vector3 worldPos3d = ScreenToWorldPoint(screenPos3d);
        Bounds meshBounds = GetImageMeshBounds();
        Vector2 normalized2d = new()
        {
            x = (worldPos3d.x - meshBounds.min.x) / meshBounds.size.x,
            y = 1.0f - (worldPos3d.y - meshBounds.min.y) / meshBounds.size.y,
        };
        return normalized2d;
    }

    void SetEyePoints(EyeRect info, ref Vector2[] eyePoints)
    {
        Vector3 pos = info.Point;
        Vector2 size = info.Size;
        eyePoints[0] = ConvertScreen3dToNormalized2d(pos + new Vector3(-size.x * 0.50f, 0.0f, 0.0f));
        eyePoints[1] = ConvertScreen3dToNormalized2d(pos + new Vector3(0.0f, -size.y * 0.50f, 0.0f));
        eyePoints[2] = ConvertScreen3dToNormalized2d(pos + new Vector3(size.x * 0.50f, 0.0f, 0.0f));
        eyePoints[3] = ConvertScreen3dToNormalized2d(pos + new Vector3(0.0f, size.y * 0.50f, 0.0f));
    }

    void SyncNormalizedFPWithScreenFP()
    {
        NormalizedFeaturePoints normFP = new()
        {
            EyeLeftPoints = new Vector2[4],
            EyeRightPoints = new Vector2[4],
            MouthPoints = new Vector2[3],
            FacePoints = new Vector2[4],
            EyeLeftAngleDegree = eyeLeftRect_.AngleDegree,
            EyeRightAngleDegree = eyeRightRect_.AngleDegree,
        };
        SetEyePoints(eyeLeftRect_, ref normFP.EyeLeftPoints);
        SetEyePoints(eyeRightRect_, ref normFP.EyeRightPoints);
        for (int cnt = 0; cnt < 3; ++cnt)
        {
            normFP.MouthPoints[cnt] = ConvertScreen3dToNormalized2d(mouthPoints_[cnt]);
        }
        for (int cnt = 0; cnt < 4; ++cnt)
        {
            normFP.FacePoints[cnt] = ConvertScreen3dToNormalized2d(facePoints_[cnt]);
        }

        normFP_ = normFP;
    }

    MeshFilter GetImageMeshFilter()
    {
        MeshFilter[] imageMFArray = meshObj_[(int)MESH_DATA_TYPE.MESH_DATA_TYPE_IMAGE].GetComponentsInChildren<MeshFilter>();
        MeshFilter imageMF = imageMFArray[0];
        return imageMF;
    }

    Bounds GetImageMeshBounds()
    {
        MeshFilter imageMF = GetImageMeshFilter();
        return imageMF.mesh.bounds;
    }

    // update vertices (assuming the image mesh is a square in the range [-1.0. 1.0])
    void UpdateImageMesh(Texture2D tex)
    {
        float yAbsValue = (float)tex.height / (float)tex.width;
        MeshFilter imageMF = GetImageMeshFilter();
        Vector3[] vertices = imageMF.mesh.vertices;
        for (int vCnt = 0; vCnt < imageMF.mesh.vertexCount; vCnt++)
        {
            int sign = (vertices[vCnt].y > 0) ? 1 : -1;
            float y = sign * yAbsValue;
            vertices[vCnt].y = y;
        }
        imageMF.mesh.SetVertices(vertices);
        imageMF.mesh.RecalculateBounds();
    }

    void SetTexture(Texture2D tex)
    {
        if (inputTexture_ != null)
        {
            Destroy(inputTexture_);
        }
        inputTexture_ = TextureUtil.Copy(tex);
    }

    public int StartEditingFP(Texture2D tex)
    {
        UpdateImageMesh(tex);

        // detect feature points from input image
        MpFaceController mpface = GameObject.Find("mpface").GetComponent<MpFaceController>();
        MpaAnalyzer.MpaRecogResult recogResult = new();
        int ret = mpface.Recognize(tex, ref recogResult);
        if (ret == 0)
        {
            // update normalized feature points with recognized result
            UpdateNormalizedFeaturePoints(recogResult, tex.width, tex.height);
        }
        else
        {
            // if failed to recognize the face, set default feature points
            normFP_ = new NormalizedFeaturePoints()
            {
                MouthPoints = fpMouthDefault,
                FacePoints = fpFaceDefault,
                EyeLeftPoints = fpEyeLeftDefault,
                EyeRightPoints = fpEyeRightDefault,
                EyeLeftAngleDegree = 0.0f,
                EyeRightAngleDegree = 0.0f,
            };
        }
        normFPOnStart_ = normFP_;
        SyncScreenFeaturePoints();

        // set texture
        SetTexture(tex);

        // update the flag
        fpEditing_ = true;

        return 0;
    }

    public int StartEditingFP(Texture2D tex, NormalizedFeaturePoints fp)
    {
        UpdateImageMesh(tex);

        normFP_ = fp;
        normFPOnStart_ = fp;
        SyncScreenFeaturePoints();

        // set texture
        SetTexture(tex);

        // update the flag
        fpEditing_ = true;

        return 0;
    }

    public int EndEditingFP()
    {
        // update the flag
        fpEditing_ = false;

        // clear selected element
        ClearSelectedElement();

        // destroy texture
        if (inputTexture_ != null)
        {
            Destroy(inputTexture_);
        }
        inputTexture_ = null;

        return 0;
    }

    void UpdateEyeElementSize(ref Vector2 size, Vector2 step, bool horizontal, bool increase)
    {
        int sign = increase ? +1 : -1;
        if (horizontal)
        {
            float sizeNew = size.x + sign * step.x;
            if (sizeNew > 0.0f)
            {
                size.x = sizeNew;
            }
        }
        else
        {
            float sizeNew = size.y + sign * step.y;
            if (sizeNew > 0.0f)
            {
                size.y = sizeNew;
            }
        }

        // update normalized feature points
        SyncNormalizedFPWithScreenFP();
    }

    public int UpdateElementSize(bool horizontal, bool increase)
    {
        int ret = 0;
        switch (selectedElement_)
        {
            case FP_ELEMENT.FP_ELEMENT_EYE_LEFT:
                UpdateEyeElementSize(ref eyeLeftRect_.Size, eyeSizeStep_, horizontal, increase);
                break;
            case FP_ELEMENT.FP_ELEMENT_EYE_RIGHT:
                UpdateEyeElementSize(ref eyeRightRect_.Size, eyeSizeStep_, horizontal, increase);
                break;
            default:
                ret = -1;
                break;
        }
        return ret;
    }

    void RotateEyeRect(ref float eyeRectAngle, bool increase)
    {
        int sign = increase ? 1 : -1;
        eyeRectAngle += sign * eyeRotateAngleDegreeStep;
        if (eyeRectAngle < eyeRotateAngleDegreeLowerLimit)
        {
            eyeRectAngle = eyeRotateAngleDegreeLowerLimit;
        }
        else if (eyeRectAngle > eyeRotateAngleDegreeUpperLimit)
        {
            eyeRectAngle = eyeRotateAngleDegreeUpperLimit;
        }
    }

    public int RotateEyeRect(bool increase)
    {
        int ret = 0;
        switch (selectedElement_)
        {
            case FP_ELEMENT.FP_ELEMENT_EYE_LEFT:
                RotateEyeRect(ref eyeLeftRect_.AngleDegree, increase);
                break;
            case FP_ELEMENT.FP_ELEMENT_EYE_RIGHT:
                RotateEyeRect(ref eyeRightRect_.AngleDegree, increase);
                break;
            default:
                ret = -1;
                break;
        }
        return ret;
    }

    MpTypes.mpVector2 ScreenToFeaturePos(Vector3 screenPos)
    {
        Vector3 worldPos = ScreenToWorldPoint(screenPos);
        Bounds meshBounds = GetImageMeshBounds();
        MpTypes.mpVector2 pos = new()
        {
            x = (worldPos.x - meshBounds.min.x) / meshBounds.size.x,
            y = 1.0f - (worldPos.y - meshBounds.min.y) / meshBounds.size.y,
        };
        return pos;
    }

    MpTypes.mpVector2 ScreenToFeatureSize(Vector2 screenSize)
    {
        Bounds meshBounds = GetImageMeshBounds();
        Vector3 screenMinPos = WorldToScreenPoint(meshBounds.min);
        Vector3 screenMaxPos = WorldToScreenPoint(meshBounds.max);
        MpTypes.mpVector2 size = new()
        {
            x = screenSize.x / (screenMaxPos.x - screenMinPos.x),
            y = -screenSize.y / (screenMaxPos.y - screenMinPos.y),
        };
        return size;
    }

    public void GetMpFeaturePoints(ref MpSynth.MpFeaturePoints mpFPPoints)
    {
        mpFPPoints.faceLeftPoint = ScreenToFeaturePos(facePoints_[0]);
        mpFPPoints.faceTopPoint = ScreenToFeaturePos(facePoints_[1]);
        mpFPPoints.faceRightPoint = ScreenToFeaturePos(facePoints_[2]);
        mpFPPoints.faceBottomPoint = ScreenToFeaturePos(facePoints_[3]);
        mpFPPoints.eyeLeftPoint = ScreenToFeaturePos(eyeLeftRect_.Point);
        mpFPPoints.eyeLeftSize = ScreenToFeatureSize(eyeLeftRect_.Size);
        mpFPPoints.eyeRightPoint = ScreenToFeaturePos(eyeRightRect_.Point);
        mpFPPoints.eyeRightSize = ScreenToFeatureSize(eyeRightRect_.Size);
        mpFPPoints.mouthLeftPoint = ScreenToFeaturePos(mouthPoints_[0]);
        mpFPPoints.mouthCenterPoint = ScreenToFeaturePos(mouthPoints_[1]);
        mpFPPoints.mouthRightPoint = ScreenToFeaturePos(mouthPoints_[2]);
        mpFPPoints.eyeLeftAngle = eyeLeftRect_.AngleDegree * Mathf.Deg2Rad;
        mpFPPoints.eyeRightAngle = eyeRightRect_.AngleDegree * Mathf.Deg2Rad;
    }

    public void RestoreCurrentFeaturePoints()
    {
        switch (curEditStep_)
        {
            case EDIT_STEP.EDIT_STEP_EYE:
                normFP_.EyeLeftPoints = normFPOnStart_.EyeLeftPoints;
                normFP_.EyeRightPoints = normFPOnStart_.EyeRightPoints;
                normFP_.EyeLeftAngleDegree = normFPOnStart_.EyeLeftAngleDegree;
                normFP_.EyeRightAngleDegree = normFPOnStart_.EyeRightAngleDegree;
                break;
            case EDIT_STEP.EDIT_STEP_MOUTH:
                normFP_.MouthPoints = normFPOnStart_.MouthPoints;
                break;
            case EDIT_STEP.EDIT_STEP_FACE:
                normFP_.FacePoints = normFPOnStart_.FacePoints;
                break;
        }
        SyncScreenFeaturePoints();
    }

    public void RestoreFeaturePoints()
    {
        normFP_ = normFPOnStart_;
        SyncScreenFeaturePoints();
    }

    public NormalizedFeaturePoints GetFeaturePoints()
    {
        return normFP_;
    }

    public void StartEditFPFlow()
    {
        curEditStep_ = EDIT_STEP.EDIT_STEP_BEGINNING + 1;
    }

    public void CancelEditFPFlow()
    {
        RestoreFeaturePoints();
        ReturnToBeginning();
    }

    public void GoPreviousStep()
    {
        if (curEditStep_ == EDIT_STEP.EDIT_STEP_BEGINNING)
        {
            return;
        }
        --curEditStep_;
        ClearSelectedElement();
    }

    public void GoNextStep()
    {
        if (curEditStep_ == EDIT_STEP.EDIT_STEP_NUM)
        {
            return;
        }
        ++curEditStep_;
        ClearSelectedElement();
        if (curEditStep_ == EDIT_STEP.EDIT_STEP_NUM)
        {
            ReturnToBeginning();
        }
    }

    public void ReturnToBeginning()
    {
        curEditStep_ = EDIT_STEP.EDIT_STEP_BEGINNING;
        ClearSelectedElement();
    }

    public bool IsBeginning()
    {
        return (curEditStep_ == EDIT_STEP.EDIT_STEP_BEGINNING);
    }

    public bool IsEyeEditing()
    {
        return (curEditStep_ == EDIT_STEP.EDIT_STEP_EYE);
    }

    // call when screen size changes
    public void NotifyScreenSizeChange()
    {
        // if screen size changes, synchronize feature points on screen
        SyncScreenFeaturePoints();
    }
}
