using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;
using System.IO;
using ExifLib;
using motionportrait;
#if !UNITY_EDITOR && UNITY_ANDROID
using UnityEngine.Android;
#endif

public class UIController : MonoBehaviour
{
    enum UI_MODE
    {
        UI_MODE_AVATAR = 0,
        UI_MODE_WEBCAM_PREVIEW,
        UI_MODE_EDIT_FP,
        UI_MODE_EDIT_FP_FLOW,
        UI_MODE_NUM,
    };

    // flag to pick input image
    private bool imagePickedFlag_;

    // texture for web camera
    private WebCamTexture webcamTexture_;

    // webcam Video Rotation Angle
    private int webcamVideoRotationAngle_;

    // input texture
    private Texture2D inputTexture_;

    // texture used to create the latest avatar
    private Texture2D avatarTexture_;

    // feature points used for the latest avatar
    private EditFPController.NormalizedFeaturePoints avatarFP_;

    // flag of feature points editing mode
    private bool fpEditing_;

    // screen size
    private Vector2Int screenSize_;

    void Start()
    {
        imagePickedFlag_ = false;
        webcamVideoRotationAngle_ = -1;
        inputTexture_ = null;
        fpEditing_ = false;
        screenSize_ = new Vector2Int(Screen.width, Screen.height);

        // sync flags with UI
        SyncUnconscious();
        SyncMouthTexture();
        SyncBackground();

        // initialize panels
        UpdatePanelsVisible(UI_MODE.UI_MODE_AVATAR);

#if UNITY_EDITOR
        // adjust layout for Unity Editor
        VerticalLayoutGroup vl = transform.GetComponentInChildren<VerticalLayoutGroup>();
        vl.padding.bottom = 0;
        vl.spacing = 0;
#endif

        // start authorization of webcam
        StartCoroutine(FindWebCamAuth());
    }

    void FindWebCam()
    {
        // find front facing camera
        WebCamDevice[] devices = WebCamTexture.devices;
        string frontFaceDeviceName = "";
        foreach (WebCamDevice d in devices)
        {
            if (d.isFrontFacing)
            {
                frontFaceDeviceName = d.name;
                break;
            }
        }

        // get webcam texture
        if (frontFaceDeviceName.Length > 0)
        {
            webcamTexture_ = new WebCamTexture(frontFaceDeviceName);
        }
    }

    IEnumerator FindWebCamAuth()
    {
#if !UNITY_EDITOR && UNITY_IOS
        // check webcam permission
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            // request webcam permission
            yield return RequestUserAuthorization(UserAuthorization.WebCam);
            yield return null;
        }

        // check webcam permission again
        if (Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            // find webcam
            FindWebCam();
        }
#elif !UNITY_EDITOR && UNITY_ANDROID
        // check webcam permission
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            // request webcam permission
            yield return RequestUserPermission(Permission.Camera);
        }

        // check webcam permission again
        if (Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            // find webcam
            FindWebCam();
        }
#else
        // find webcam
        FindWebCam();
#endif
        yield break;
    }

#if !UNITY_EDITOR && UNITY_IOS
    IEnumerator RequestUserAuthorization(UserAuthorization mode)
    {
        yield return Application.RequestUserAuthorization(mode);
    }
#elif !UNITY_EDITOR && UNITY_ANDROID
    IEnumerator RequestUserPermission(string permission)
    {
        Permission.RequestUserPermission(permission);
        yield break;
    }
#endif

    bool Contains(Component c, Vector2 mousePos)
    {
        RectTransform t = c.GetComponent<RectTransform>();
        if (mousePos.x < t.position.x - t.sizeDelta.x / 2) return false;
        if (mousePos.x > t.position.x + t.sizeDelta.x / 2) return false;
        if (mousePos.y < t.position.y - t.sizeDelta.y / 2) return false;
        if (mousePos.y > t.position.y + t.sizeDelta.y / 2) return false;
        return true;
    }

    MpFaceController GetFaceController()
    {
        MpFaceController mpface = GameObject.Find("mpface").GetComponent<MpFaceController>();
        return mpface;
    }

    EditFPController GetEditFPController()
    {
        EditFPController editfp = GameObject.Find("EditFP").GetComponent<EditFPController>();
        return editfp;
    }

    bool HitTestUIComponents(Vector2 mousePos)
    {
        Button[] buttonArray = transform.GetComponentsInChildren<Button>();
        for (int i = 0; i < buttonArray.Length; ++i)
        {
            Button c = buttonArray[i];
            if (Contains(c, mousePos))
            {
                return true;
            }
        }
        Toggle[] toggleArray = transform.GetComponentsInChildren<Toggle>();
        for (int i = 0; i < toggleArray.Length; ++i)
        {
            Toggle c = toggleArray[i];
            if (Contains(c, mousePos))
            {
                return true;
            }
        }
        return false;
    }

    void Update()
    {
        // touch/mouse event
        bool down = false;
        bool move = false;
        bool up = false;
        Vector2 mousePos = Vector2.zero;
        bool available = true;
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            down = touchscreen.primaryTouch.press.wasPressedThisFrame;
            up = touchscreen.primaryTouch.press.wasReleasedThisFrame;
            move = touchscreen.primaryTouch.press.isPressed;
            mousePos = touchscreen.primaryTouch.position.ReadValue();
        }
        else
        {
            available = false;
        }
#else
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            down = mouse.leftButton.wasPressedThisFrame;
            up = mouse.leftButton.wasReleasedThisFrame;
            move = mouse.leftButton.isPressed;
            mousePos = mouse.position.ReadValue();
        }
#endif

        if (fpEditing_)
        {
            EditFPController editFPCtrl = GetEditFPController();
            Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
            if (screenSize != screenSize_)
            {
                editFPCtrl.NotifyScreenSizeChange();
                screenSize_ = screenSize;
            }

            if (available)
            {
                if (!HitTestUIComponents(mousePos))
                {
                    editFPCtrl.UpdateEditFP(down, up, mousePos);
                }
            }
        }
        else
        {
            if (down || move)
            {
                if (!HitTestUIComponents(mousePos))
                {
                    // change face orientation
                    MpTypes.mpVector2 normPos = new()
                    {
                        x = mousePos.x / Screen.width,
                        y = mousePos.y / Screen.height
                    };
                    MpFaceController mpfaceCtrl = GetFaceController();
                    mpfaceCtrl.LookAt(0, ref normPos, 1.0f);
                }
            }
            if (up)
            {
                // change face orientation
                MpTypes.mpVector2 normPos = new()
                {
                    x = 0.5f,
                    y = 0.5f
                };
                MpFaceController mpfaceCtrl = GetFaceController();
                mpfaceCtrl.LookAt(500, ref normPos, 1.0f);
            }
        }

        if (imagePickedFlag_)
        {
            if (!NativeGallery.IsMediaPickerBusy())
            {
                PickImage(1024);
                imagePickedFlag_ = false;
            }
        }

        if (webcamTexture_ != null)
        {
            if (webcamTexture_.isPlaying)
            {
                if (webcamTexture_.didUpdateThisFrame)
                {
                    if (webcamVideoRotationAngle_ != webcamTexture_.videoRotationAngle)
                    {
                        UpdateWebCamRawImage();
                        webcamVideoRotationAngle_ = webcamTexture_.videoRotationAngle;
                    }
                }
            }
        }
    }

    Texture2D CreateReadabeTexture2D(Texture2D texture2d)
    {
        RenderTexture renderTexture = RenderTexture.GetTemporary(
            texture2d.width,
            texture2d.height,
            0,
            RenderTextureFormat.Default,
            RenderTextureReadWrite.Linear);

        Graphics.Blit(texture2d, renderTexture);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTexture;
        Texture2D readableTexture2D = new(texture2d.width, texture2d.height);
        readableTexture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        readableTexture2D.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTexture);
        return readableTexture2D;
    }

    bool IsJpeg(string path)
    {
        string[] extArray = { ".jpg", ".jpeg" };
        foreach (string ext in extArray)
        {
            if (path.EndsWith(ext, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    // pick the image
    void PickImage(int maxSize)
    {
#if !UNITY_EDITOR && (UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX)
        // NativeGallery only supports Android/iOS, so use StandaloneFileBrowser on desktop runtimes.
        SFB.ExtensionFilter[] extensions = new[] {
            new SFB.ExtensionFilter("Image Files", "png", "jpg", "jpeg"),
        };
        string[] paths = SFB.StandaloneFileBrowser.OpenFilePanel("Pick Image", "", extensions, false);
        if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
        {
            OnImagePicked(paths[0], maxSize);
        }
#else
        NativeGallery.Permission permission = NativeGallery.GetImageFromGallery((path) =>
        {
            if (path != null)
            {
                OnImagePicked(path, maxSize);
            }
        });
        Debug.Log("[PickImage] Permission result: " + permission);
#endif
    }

    void OnImagePicked(string path, int maxSize)
    {
        // load the image
        Texture2D texture = NativeGallery.LoadImageAtPath(path, maxSize);
        if (texture == null)
        {
            Debug.Log($"[PickImage] Failed to load texture: {path}");
            return;
        }
        texture = CreateReadabeTexture2D(texture);

        if (IsJpeg(path))
        {
            // load selected image file
            FileStream fileStream = new(path, FileMode.Open, FileAccess.Read);
            BinaryReader bin = new(fileStream);
            byte[] values = bin.ReadBytes((int)bin.BaseStream.Length);
            bin.Close();

            // get exif
            string filename = Path.GetFileName(path);
            JpegInfo info = ExifReader.ReadJpeg(values, filename);
            ExifOrientation ori = info.Orientation;

            // rotate the image according to exif information
            int rotAngle = 0;
            switch (ori)
            {
                case ExifOrientation.TopRight:
                    // rotate 90-degree clockwise
                    rotAngle = 90;
                    break;
                case ExifOrientation.BottomLeft:
                    // rotate 90-degree counter-clockwise
                    rotAngle = 270;
                    break;
                case ExifOrientation.BottomRight:
                    // rotate 180-degree
                    rotAngle = 180;
                    break;
                default:
                    break;
            }

            int w = texture.width;
            int h = texture.height;
            Color32[] colorArray = new Color32[texture.width * texture.height];
            TextureUtil.RotateImage(rotAngle, texture.GetPixels32(), texture.width, texture.height, ref colorArray, ref w, ref h);
            texture = new(w, h);
            texture.SetPixels32(colorArray);
            texture.Apply();
        }

        // start editing feature points
        StartEditingFP(texture);
    }

    void UpdatePanelsVisible(UI_MODE mode)
    {
        bool webcamPreviewPanelVisible = false;
        bool eyeCtrlPanelVisible = false;
        bool editFPPanelVisible = false;
        bool editFPFlowPanelVisible = false;
        bool buttonPanelVisible = false;
        bool togglePanelVisible = false;
        EditFPController editFPCtrl = GetEditFPController();
        switch (mode)
        {
            case UI_MODE.UI_MODE_AVATAR:
                buttonPanelVisible = togglePanelVisible = true;
                break;
            case UI_MODE.UI_MODE_WEBCAM_PREVIEW:
                webcamPreviewPanelVisible = true;
                break;
            case UI_MODE.UI_MODE_EDIT_FP:
                editFPPanelVisible = true;
                break;
            case UI_MODE.UI_MODE_EDIT_FP_FLOW:
                editFPFlowPanelVisible = true;
                if (editFPCtrl.IsEyeEditing())
                {
                    eyeCtrlPanelVisible = true;
                }
                break;
        }
        transform.Find("WebCamPreviewPanel").gameObject.SetActive(webcamPreviewPanelVisible);
        transform.Find("EyeCtrlParentPanel").gameObject.SetActive(eyeCtrlPanelVisible);
        transform.Find("EditFPPanel").gameObject.SetActive(editFPPanelVisible);
        transform.Find("EditFPFlowPanel").gameObject.SetActive(editFPFlowPanelVisible);
        transform.Find("ButtonPanel").gameObject.SetActive(buttonPanelVisible);
        transform.Find("TogglePanel").gameObject.SetActive(togglePanelVisible);
    }

    void StartEditingFP(Texture2D texture)
    {
        // Defer by one frame so the click that dismisses the file dialog
        // isn't delivered to the edit panel's OK/Cancel buttons.
        StartCoroutine(StartEditingFPCoroutine(texture));
    }

    IEnumerator StartEditingFPCoroutine(Texture2D texture)
    {
        // wait one frame so the residual click is consumed before the panel appears
        yield return null;

        fpEditing_ = true;

        // clear expression and voice
        ClearExpression();
        ClearVoice();

        // update texture
        if (inputTexture_ != null)
        {
            Destroy(inputTexture_);
        }
        inputTexture_ = TextureUtil.Copy(texture);

        // show/hide UI components
        UpdatePanelsVisible(UI_MODE.UI_MODE_EDIT_FP);

        // set inactive MpFaceController
        MpFaceController mpfaceCtrl = GetFaceController();
        mpfaceCtrl.StartEditingFP();

        // start editing feature points
        EditFPController editFPCtrl = GetEditFPController();
        editFPCtrl.StartEditingFP(inputTexture_);
    }

    void EndEditingFP()
    {
        fpEditing_ = false;

        // show/hide UI components
        UpdatePanelsVisible(UI_MODE.UI_MODE_AVATAR);

        // set active MpFaceController
        MpFaceController mpfaceCtrl = GetFaceController();
        mpfaceCtrl.EndEditingFP();

        // start editing feature points
        EditFPController editFPCtrl = GetEditFPController();
        editFPCtrl.EndEditingFP();
    }

    void UpdateWebCamRawImage()
    {
        Transform webCamRawImageTransform = transform.Find("WebCamPreviewPanel").Find("WebCamRawImage");
        RawImage rawImage = webCamRawImageTransform.GetComponent<RawImage>();
        webCamRawImageTransform.gameObject.SetActive(true);

        int wh = Screen.width < Screen.height ? Screen.width - 100 : Screen.height - 100;
        Vector2 originalSize = new Vector2(wh, wh);
        Vector2 textureSize = new Vector2(rawImage.texture.width, rawImage.texture.height);

        float heightScale = originalSize.y / textureSize.y;
        float widthScale = originalSize.x / textureSize.x;
        Vector2 rectSize = textureSize * Mathf.Min(heightScale, widthScale);

        Vector2 anchorDiff = rawImage.rectTransform.anchorMax - rawImage.rectTransform.anchorMin;
        Vector2 parentSize = (rawImage.transform.parent as RectTransform).rect.size;
        Vector2 anchorSize = parentSize * anchorDiff;

        // RawImage size and position
        rawImage.rectTransform.sizeDelta = rectSize - anchorSize;
        rawImage.rectTransform.anchoredPosition = new Vector3(0.0f, wh / 2 + 100, 0.0f);

        // RawImage rotation
        webCamRawImageTransform.rotation = Quaternion.AngleAxis(webcamTexture_.videoRotationAngle, Vector3.back);
        rawImage.uvRect = webcamTexture_.videoVerticallyMirrored ? new Rect(0.0f, 1.0f, 1.0f, -1.0f) : new Rect(0.0f, 0.0f, 1.0f, 1.0f);
    }

    public void OnPhotoButtonClicked()
    {
        imagePickedFlag_ = true;
    }

    public void OnCameraButtonClicked()
    {
        if (webcamTexture_ == null)
        {
            Debug.Log("Webcam texture is not available.");
            return;
        }

        // Webcam play
        webcamTexture_.Play();

        // show/hide UI components
        UpdatePanelsVisible(UI_MODE.UI_MODE_WEBCAM_PREVIEW);

        // set preview texture
        Transform webCamRawImageTransform = transform.Find("WebCamPreviewPanel").Find("WebCamRawImage");
        RawImage rawImage = webCamRawImageTransform.GetComponent<RawImage>();
        rawImage.texture = webcamTexture_;
        webCamRawImageTransform.gameObject.SetActive(false);

        webcamVideoRotationAngle_ = -1;

        // set active MpFaceController
        MpFaceController mpfaceCtrl = GetFaceController();
        mpfaceCtrl.SetMeshActive(false);
    }

    public void OnWebCamShutterButtonClicked()
    {
        // clear expression and voice
        ClearExpression();
        ClearVoice();

        // get texture
        Texture2D texture = TextureUtil.Copy(webcamTexture_);

        // start editing feature points
        StartEditingFP(texture);

        // Webcam stop
        webcamTexture_.Stop();
    }

    public void OnWebCamCancelButtonClicked()
    {
        // Webcam stop
        webcamTexture_.Stop();

        // show/hide UI components
        UpdatePanelsVisible(UI_MODE.UI_MODE_AVATAR);

        // set active MpFaceController
        MpFaceController mpfaceCtrl = GetFaceController();
        mpfaceCtrl.SetMeshActive(true);
    }

    public void OnEditFPButtonClicked()
    {
        if (avatarTexture_ == null || avatarFP_ == null)
        {
            return;
        }

        fpEditing_ = true;

        // clear expression and voice
        ClearExpression();
        ClearVoice();

        // show/hide UI components
        UpdatePanelsVisible(UI_MODE.UI_MODE_EDIT_FP);

        // set inactive MpFaceController
        MpFaceController mpfaceCtrl = GetFaceController();
        mpfaceCtrl.StartEditingFP();

        // start editing feature points
        EditFPController editFPCtrl = GetEditFPController();
        editFPCtrl.StartEditingFP(avatarTexture_, avatarFP_);
    }

    void ClearExpression()
    {
        MpFaceController mpfaceCtrl = GetFaceController();
        mpfaceCtrl.ClearExpression();
    }

    void ClearVoice()
    {
        MpFaceController mpfaceCtrl = GetFaceController();
        mpfaceCtrl.ClearVoice();
    }

    public void OnExpressionButtonClicked()
    {
        ClearVoice();
        MpFaceController mpfaceCtrl = GetFaceController();
        mpfaceCtrl.UpdateExpression();
    }

    public void OnVoiceButtonClicked()
    {
        ClearExpression();
        MpFaceController mpfaceCtrl = GetFaceController();
        mpfaceCtrl.StartLipSync();
    }

    Toggle FindToggle(string name)
    {
        Transform pt = transform.Find("TogglePanel");
        Toggle t = pt.Find(name).GetComponent<Toggle>();
        return t;
    }

    void SyncUnconscious()
    {
        Toggle t = FindToggle("UnconsciousToggle");
        MpFaceController mpfaceCtrl = GetFaceController();
        mpfaceCtrl.EnableUnconscious(t.isOn);
    }

    public void OnUnconsciousValueChanged()
    {
        SyncUnconscious();
    }

    void SyncMouthTexture()
    {
        Toggle t = FindToggle("MouthTextureToggle");
        MpFaceController mpfaceCtrl = GetFaceController();
        mpfaceCtrl.ReplaceMouthTexture(t.isOn);
    }

    public void OnMouthTextureValueChanged()
    {
        SyncMouthTexture();
    }

    void SyncBackground()
    {
        Toggle t = FindToggle("BackgroundToggle");
        MpFaceController mpfaceCtrl = GetFaceController();
        mpfaceCtrl.EnableBackground(t.isOn);
    }

    public void OnBackgroundValueChanged()
    {
        SyncBackground();
    }

    public void OnEditFPOKButtonClicked()
    {
        // get feature points
        EditFPController editFPCtrl = GetEditFPController();
        MpSynth.MpFeaturePoints mpFPPoints = new();
        editFPCtrl.GetMpFeaturePoints(ref mpFPPoints);

        // finish editing feature points
        EndEditingFP();

        // create avatar
        MpFaceController mpfaceCtrl = GetFaceController();
        int ret = mpfaceCtrl.CreateAvatar(inputTexture_, mpFPPoints);
        if (ret == 0)
        {
            if (avatarTexture_ != null)
            {
                Destroy(avatarTexture_);
            }
            avatarTexture_ = TextureUtil.Copy(inputTexture_);
            avatarFP_ = editFPCtrl.GetFeaturePoints();
        }
    }

    public void OnEditFPStartButtonClicked()
    {
        EditFPController editFPCtrl = GetEditFPController();
        editFPCtrl.StartEditFPFlow();
        UpdatePanelsVisible(UI_MODE.UI_MODE_EDIT_FP_FLOW);
    }

    public void OnEditFPResetButtonClicked()
    {
        EditFPController editFPCtrl = GetEditFPController();
        editFPCtrl.RestoreFeaturePoints();
    }

    public void OnEditFPCancelButtonClicked()
    {
        EndEditingFP();

        if (avatarTexture_ != null)
        {
            if (inputTexture_ != null)
            {
                Destroy(inputTexture_);
            }

            // restore input texture
            inputTexture_ = TextureUtil.Copy(avatarTexture_);
        }
    }

    public void OnEditFPFlowPrevButtonClicked()
    {
        EditFPController editFPCtrl = GetEditFPController();
        editFPCtrl.GoPreviousStep();
        if (editFPCtrl.IsBeginning())
        {
            UpdatePanelsVisible(UI_MODE.UI_MODE_EDIT_FP);
        }
        else
        {
            UpdatePanelsVisible(UI_MODE.UI_MODE_EDIT_FP_FLOW);
        }
    }

    public void OnEditFPFlowNextButtonClicked()
    {
        EditFPController editFPCtrl = GetEditFPController();
        editFPCtrl.GoNextStep();
        if (editFPCtrl.IsBeginning())
        {
            UpdatePanelsVisible(UI_MODE.UI_MODE_EDIT_FP);
        }
        else
        {
            UpdatePanelsVisible(UI_MODE.UI_MODE_EDIT_FP_FLOW);
        }
    }

    public void OnEditFPFlowResetButtonClicked()
    {
        EditFPController editFPCtrl = GetEditFPController();
        editFPCtrl.RestoreCurrentFeaturePoints();
    }

    public void OnEditFPFlowCancelButtonClicked()
    {
        EditFPController editFPCtrl = GetEditFPController();
        editFPCtrl.CancelEditFPFlow();
        UpdatePanelsVisible(UI_MODE.UI_MODE_EDIT_FP);
    }

    public void OnWidthPlusButtonClicked()
    {
        EditFPController editFPCtrl = GetEditFPController();
        editFPCtrl.UpdateElementSize(true, true);
    }

    public void OnWidthMinusButtonClicked()
    {
        EditFPController editFPCtrl = GetEditFPController();
        editFPCtrl.UpdateElementSize(true, false);
    }

    public void OnHeightPlusButtonClicked()
    {
        EditFPController editFPCtrl = GetEditFPController();
        editFPCtrl.UpdateElementSize(false, true);
    }

    public void OnHeightMinusButtonClicked()
    {
        EditFPController editFPCtrl = GetEditFPController();
        editFPCtrl.UpdateElementSize(false, false);
    }

    public void OnRotationPlusButtonClicked()
    {
        EditFPController editFPCtrl = GetEditFPController();
        editFPCtrl.RotateEyeRect(true);
    }

    public void OnRotationMinusButtonClicked()
    {
        EditFPController editFPCtrl = GetEditFPController();
        editFPCtrl.RotateEyeRect(false);
    }
}
