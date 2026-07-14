using UnityEngine;
using UnityEngine.Networking;
using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using motionportrait;

public class MpFaceController : MonoBehaviour
{
    enum MESH_DATA_TYPE
    {
        MESH_DATA_TYPE_FACE = 0,
        MESH_DATA_TYPE_LIP,
        MESH_DATA_TYPE_RIGHT_EYELID,
        MESH_DATA_TYPE_LEFT_EYELID,
        MESH_DATA_TYPE_LOWER_TEETH,
        MESH_DATA_TYPE_UPPER_TEETH,
        MESH_DATA_TYPE_BACKGROUND,
        MESH_DATA_TYPE_NUM,
    };

    enum FACE_NAME
    {
        [InspectorName("face0")]
        FACE0 = 0,
        [InspectorName("face1")]
        FACE1,
        [InspectorName("face2")]
        FACE2,
    };

    enum VOICE_NAME
    {
        [InspectorName("voice0")]
        VOICE0 = 0,
        [InspectorName("voice1")]
        VOICE1,
        [InspectorName("voice2")]
        VOICE2,
    };

    [SerializeField] private bool enableTeethExposure_;     // set to Inspector
    [SerializeField] private bool preloadAvatar_;           // set to Inspector
    [SerializeField] private FACE_NAME faceName_ = FACE_NAME.FACE0;     // set to Inspector
    [SerializeField] private VOICE_NAME voiceName_ = VOICE_NAME.VOICE0; // set to Inspector

    private bool enableUnconscious_;
    private bool replaceMouthTexture_;
    private bool enableBackground_;

    private bool enableUnconsciousPrev_;
    private bool enableTeethExposurePrev_;

    private readonly int DESTROY_TEXTURES_RENDER_NUM = (int)MESH_DATA_TYPE.MESH_DATA_TYPE_NUM - 1;

    private GameObject[] meshObj_;
    private int faceVertexNum_;

    private string resourcePath_;

    private MpMesh mpMesh_;
    private MpFace mpFace_;
    private MpAnimation mpAnimation_;
    private MpSpeech mpSpeech_;
#if false
	private MpCosme mpCosme_;
#endif
    private MpSynth mpSynth_;
    private MpaAnalyzer mpaAnalyzer_;

    private bool isFaceReady_;
    private bool isBlinkEnable_;
    private float blinkGainLeft_;
    private float blinkGainRight_;
    private float unconsciousGain_;
    private UIntPtr voiceId_;
#if false
	private UIntPtr cosmeId_;
#endif
    private int exprId_;
    private FACE_NAME faceNamePrev_;
    private System.DateTime startTime_;

    // flag of feature points editing mode
    private bool fpEditing_;

    // Use this for initialization
    void Start()
    {
        // mesh
        meshObj_ = new GameObject[(int)MESH_DATA_TYPE.MESH_DATA_TYPE_NUM];
        meshObj_[(int)MESH_DATA_TYPE.MESH_DATA_TYPE_FACE] = transform.Find("Face").gameObject;      // facelip40x40.obj
        meshObj_[(int)MESH_DATA_TYPE.MESH_DATA_TYPE_LIP] = transform.Find("Lip").gameObject;            // facelip40x40.obj
        meshObj_[(int)MESH_DATA_TYPE.MESH_DATA_TYPE_RIGHT_EYELID] = transform.Find("RightEyelid").gameObject;   // eyelid10x8.obj
        meshObj_[(int)MESH_DATA_TYPE.MESH_DATA_TYPE_LEFT_EYELID] = transform.Find("LeftEyelid").gameObject; // eyelid10x8.obj
        meshObj_[(int)MESH_DATA_TYPE.MESH_DATA_TYPE_LOWER_TEETH] = transform.Find("LowerTeeth").gameObject; // teeth8x4.obj
        meshObj_[(int)MESH_DATA_TYPE.MESH_DATA_TYPE_UPPER_TEETH] = transform.Find("UpperTeeth").gameObject;  // teeth8x4.obj
        meshObj_[(int)MESH_DATA_TYPE.MESH_DATA_TYPE_BACKGROUND] = transform.Find("Background").gameObject;  // background.obj

        enableUnconsciousPrev_ = enableUnconscious_;
        enableTeethExposurePrev_ = enableTeethExposure_;

        faceVertexNum_ = 0;

        string assetsPath = Application.streamingAssetsPath + "/MotionPortrait/Resources.zip";
        string archivePath = "";
#if !UNITY_EDITOR && UNITY_ANDROID
        // load resource archive
        UnityWebRequest www = UnityWebRequest.Get(assetsPath);
        www.SendWebRequest();
        while (!www.isDone)
        {
        }
        archivePath = Application.persistentDataPath + "/Resources.zip";
        File.WriteAllBytes(archivePath, www.downloadHandler.data);
#else
        archivePath = assetsPath;
#endif
        string resourceDirPath = Application.persistentDataPath + "/Resources";

        // remove old resource folder
        if (Directory.Exists(resourceDirPath))
        {
            Directory.Delete(resourceDirPath, true);
        }

        // extract resources
        DirectoryInfo parentInfo = Directory.GetParent(resourceDirPath);
        ZipFile.ExtractToDirectory(archivePath, parentInfo.ToString());

        // set resourcePath
        resourcePath_ = resourceDirPath;

        // init motionportrait classes
        mpMesh_ = new MpMesh();
        MpMesh.Context meshContext;
        meshContext.faceMeshDiv = 40;
        mpMesh_.Init(ref meshContext);

        mpFace_ = new MpFace();
        mpAnimation_ = mpFace_.GetCtlAnimation();
        mpSpeech_ = mpFace_.GetCtlSpeech();
#if false
		mpCosme_ = new MpCosme ();
#endif

        string synthResPath = resourcePath_ + "/res";
        mpSynth_ = new MpSynth();
        int retSynth = mpSynth_.Init(synthResPath);
        if (retSynth != 0)
        {
            Debug.Log("[Start] Failed to initialize MpSynth.");
        }
        mpaAnalyzer_ = new MpaAnalyzer();
        int retAnalyzer = mpaAnalyzer_.Init(synthResPath);
        if (retAnalyzer != 0)
        {
            Debug.Log("[Start] Failed to initialize MpaAnalyzer.");
        }

        // set synthesis parameters
        mpSynth_.SetParami(MpSynth.Param.MODEL_SIZE, 512);
        mpSynth_.SetParami(MpSynth.Param.TEX_SIZE, 512);
        mpSynth_.SetParamf(MpSynth.Param.FACE_SIZE, 0.50f);
        mpSynth_.SetParamf(MpSynth.Param.FACE_POS, 0.50f);
        mpSynth_.SetParami(MpSynth.Param.CROP_MARGIN, 0);
        mpSynth_.SetParami(MpSynth.Param.FILL_MARGIN, 1);

        // init params
        isFaceReady_ = false;
        isBlinkEnable_ = false;
        blinkGainLeft_ = 0.0F;
        blinkGainRight_ = 0.0F;
        unconsciousGain_ = 1.0F;
        voiceId_ = UIntPtr.Zero;
#if false
		cosmeId_ = UIntPtr.Zero;
#endif
        startTime_ = System.DateTime.Now;

        exprId_ = 0;
        faceNamePrev_ = faceName_;

        fpEditing_ = false;

        // preload an avatar
        if (preloadAvatar_)
        {
            int faceIndex = (int)faceName_;
            string facebinPath = resourcePath_ + "/face/face" + faceIndex.ToString() + ".bin";
            LoadFaceData(facebinPath);
        }
    }

    // recognize a face in given image
    public int Recognize(Texture2D tex, ref MpaAnalyzer.MpaRecogResult recogResult)
    {
        MpaAnalyzer.MpaImage image = ToAnalyzerImage(tex);
        int ret = mpaAnalyzer_.SetImage(image);
        if (ret != 0)
        {
            Debug.Log("MpaAnalyzer::SetImage() failed.");
            return -1;
        }
        ret = mpaAnalyzer_.Recognize(ref recogResult);
        if (ret != 0)
        {
            Debug.Log("MpaAnalyzer::Recognize() failed.");
            return -1;
        }
        return 0;
    }

    public void LookAt(int msec, ref MpTypes.mpVector2 position, float weight)
    {
        if (isFaceReady_)
        {
            mpAnimation_.LookAt(msec, ref position, weight);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!fpEditing_)
        {
            // show/hide background
            meshObj_[(int)MESH_DATA_TYPE.MESH_DATA_TYPE_BACKGROUND].SetActive(enableBackground_);
        }

        // reload face
        if (faceNamePrev_ != faceName_)
        {
            faceNamePrev_ = faceName_;
            int faceIndex = (int)faceName_;
            string facebinPath = resourcePath_ + "/face/face" + faceIndex.ToString() + ".bin";
            LoadFaceData(facebinPath);
        }

        // update face
        if (isFaceReady_)
        {
            int msec = Getmsec();

            // unconscious
            if (enableUnconsciousPrev_ != enableUnconscious_)
            {
                if (enableUnconscious_)
                {
                    mpAnimation_.SetUnconsciousGain(unconsciousGain_);
                }
                else
                {
                    mpAnimation_.SetUnconsciousGain(0.0F);
                }
                enableUnconsciousPrev_ = enableUnconscious_;
            }

            // teeth exposure
            if (enableTeethExposurePrev_ != enableTeethExposure_)
            {
                mpFace_.UseAutoExposure(MpFace.ExposureTarget.EXPOSURE_TARGET_TEETH, enableTeethExposure_);
                UpdateTextures();
                enableTeethExposurePrev_ = enableTeethExposure_;
            }

            // update animation
            mpAnimation_.UpdateAnimation(msec);

            // update mesh
            if (!isBlinkEnable_)
            {
                mpMesh_.Update();
            }
            else
            {
                mpMesh_.Update(blinkGainRight_, blinkGainLeft_);
            }
            UpdateModels();
        }
    }

    // Load Face Data
    public void LoadFaceData(string facebinPath)
    {
        // destroy face
        DestroyFace();

        // load face
        int ret = mpFace_.Load(facebinPath);
        if (ret != 0)
        {
            Debug.Log("LoadFaceData(): can't create face.");
            return;
        }
        if (enableUnconscious_)
        {
            mpAnimation_.SetUnconsciousGain(unconsciousGain_);
        }
        else
        {
            mpAnimation_.SetUnconsciousGain(0.0F);
        }
        mpFace_.UseAutoExposure(MpFace.ExposureTarget.EXPOSURE_TARGET_TEETH, enableTeethExposure_);

        // set expression data
        string exprFilePath = resourcePath_ + "/expr/faceanim.txt";
        mpAnimation_.SetExprData(exprFilePath);

        // set face
        mpMesh_.SetFace(mpFace_);

        // set unconscious animation parameters
        mpAnimation_.SetParamf(MpAnimation.Param.NECK_X_MAX_ROT, 2.0f);
        mpAnimation_.SetParamf(MpAnimation.Param.NECK_Y_MAX_ROT, 2.0f);
        mpAnimation_.SetParamf(MpAnimation.Param.NECK_Z_MAX_ROT, 0.30f);

        faceVertexNum_ = mpMesh_.GetPartsMaxVertexNum(MpMesh.MeshParts.MESH_PARTS_FACE);
        int trianglesNum = mpMesh_.GetPartsMaxTrianglesNum(MpMesh.MeshParts.MESH_PARTS_FACE);

        int eyelidVertexNum = mpMesh_.GetPartsMaxVertexNum(MpMesh.MeshParts.MESH_PARTS_RIGHT_EYELID);
        int eyelidTrianglesNum = mpMesh_.GetPartsMaxTrianglesNum(MpMesh.MeshParts.MESH_PARTS_RIGHT_EYELID);

        int lowerTeethVertexNum = mpMesh_.GetPartsMaxVertexNum(MpMesh.MeshParts.MESH_PARTS_LOWER_TEETH);
        int lowerTeethTrianglesNum = mpMesh_.GetPartsMaxTrianglesNum(MpMesh.MeshParts.MESH_PARTS_LOWER_TEETH);

        int upperTeethVertexNum = mpMesh_.GetPartsMaxVertexNum(MpMesh.MeshParts.MESH_PARTS_UPPER_TEETH);
        int upperTeethTrianglesNum = mpMesh_.GetPartsMaxTrianglesNum(MpMesh.MeshParts.MESH_PARTS_UPPER_TEETH);

        int[] meshDataIndex = new int[6] { (int)MESH_DATA_TYPE.MESH_DATA_TYPE_FACE, (int)MESH_DATA_TYPE.MESH_DATA_TYPE_LIP, (int)MESH_DATA_TYPE.MESH_DATA_TYPE_RIGHT_EYELID, (int)MESH_DATA_TYPE.MESH_DATA_TYPE_LEFT_EYELID, (int)MESH_DATA_TYPE.MESH_DATA_TYPE_LOWER_TEETH, (int)MESH_DATA_TYPE.MESH_DATA_TYPE_UPPER_TEETH };
        MpMesh.MeshData[] mpMeshData = new MpMesh.MeshData[6];      // 0:Face, 1:Lip, 2:RightEyelid, 3:LeftEyelid, 4:LowerTeeth, 5:UpperTeeth
        float[][] meshVertex = new float[6][];
        float[][] meshTexcoord = new float[6][];
        ushort[][] meshIndices = new ushort[6][];
        GCHandle[] handleMeshVertex = new GCHandle[6];
        GCHandle[] handleMeshTexcoord = new GCHandle[6];
        GCHandle[] handleMeshIndices = new GCHandle[6];
        for (int i = 0; i < 2; i++)
        {
            meshVertex[i] = new float[faceVertexNum_ * 3];
            meshTexcoord[i] = new float[faceVertexNum_ * 2];
            meshIndices[i] = new ushort[trianglesNum * 3];
        }
        for (int i = 2; i < 4; i++)
        {
            meshVertex[i] = new float[eyelidVertexNum * 3];
            meshTexcoord[i] = new float[eyelidVertexNum * 2];
            meshIndices[i] = new ushort[eyelidTrianglesNum * 3];
        }
        for (int i = 4; i < 5; i++)
        {
            meshVertex[i] = new float[lowerTeethVertexNum * 3];
            meshTexcoord[i] = new float[lowerTeethVertexNum * 2];
            meshIndices[i] = new ushort[lowerTeethTrianglesNum * 3];
        }
        for (int i = 5; i < 6; i++)
        {
            meshVertex[i] = new float[upperTeethVertexNum * 3];
            meshTexcoord[i] = new float[upperTeethVertexNum * 2];
            meshIndices[i] = new ushort[upperTeethTrianglesNum * 3];
        }
        for (int i = 0; i < 6; i++)
        {
            // convert float[] to IntPtr
            handleMeshVertex[i] = GCHandle.Alloc(meshVertex[i], GCHandleType.Pinned);
            handleMeshTexcoord[i] = GCHandle.Alloc(meshTexcoord[i], GCHandleType.Pinned);
            handleMeshIndices[i] = GCHandle.Alloc(meshIndices[i], GCHandleType.Pinned);

            mpMeshData[i].vertexNum = faceVertexNum_;
            mpMeshData[i].vertex = handleMeshVertex[i].AddrOfPinnedObject();
            mpMeshData[i].normal = IntPtr.Zero;
            mpMeshData[i].texcoord = handleMeshTexcoord[i].AddrOfPinnedObject();
            mpMeshData[i].vertexFixed = IntPtr.Zero;
            mpMeshData[i].trianglesNum = trianglesNum;
            mpMeshData[i].indices = handleMeshIndices[i].AddrOfPinnedObject();
        }

        // get mesh
        MpMesh.MeshConfig config;
        config.type = MpMesh.MeshType.MESH_TYPE_RELIEF;
        config.direction = MpMesh.MeshDirection.MESH_DIRECTION_Z_PLUS;
        config.offset.x = 0.0F;
        config.offset.y = 0.0F;
        config.offset.z = 0.0F;
        config.scale = 0.5F;
        config.depthScale = 0.5F;
        config.smoothMeshIterations = 0;
        mpMesh_.GetPartsMesh(MpMesh.MeshParts.MESH_PARTS_FACE, ref mpMeshData[0], ref config);
        mpMesh_.GetPartsMesh(MpMesh.MeshParts.MESH_PARTS_LIP, ref mpMeshData[1], ref config);
        mpMesh_.GetPartsMesh(MpMesh.MeshParts.MESH_PARTS_RIGHT_EYELID, ref mpMeshData[2], ref config);
        mpMesh_.GetPartsMesh(MpMesh.MeshParts.MESH_PARTS_LEFT_EYELID, ref mpMeshData[3], ref config);
        mpMesh_.GetPartsMesh(MpMesh.MeshParts.MESH_PARTS_LOWER_TEETH, ref mpMeshData[4], ref config);
        mpMesh_.GetPartsMesh(MpMesh.MeshParts.MESH_PARTS_UPPER_TEETH, ref mpMeshData[5], ref config);

        // update texcoord
        for (int i = 0; i < 2; i++)
        {
            MeshFilter[] mf = meshObj_[meshDataIndex[i]].GetComponentsInChildren<MeshFilter>();

            // convert IntPtr to float[]
            float[] mpTexcoord = new float[faceVertexNum_ * 2];
            Marshal.Copy(mpMeshData[i].texcoord, mpTexcoord, 0, faceVertexNum_ * 2);

            int index = 0;
            for (int j = 0; j < mf.Length; j++)
            {
                Vector2[] uv = mf[j].mesh.uv;
                for (int k = 0; k < mf[j].mesh.vertexCount; k++)
                {
                    uv[k] = new Vector2(1.0F - mpTexcoord[index * 2 + 0], mpTexcoord[index * 2 + 1]);
                    index++;
                }
                mf[j].mesh.uv = uv;
            }
        }
        for (int i = 2; i < 4; i++)
        {
            MeshFilter[] mf = meshObj_[meshDataIndex[i]].GetComponentsInChildren<MeshFilter>();

            // convert IntPtr to float[]
            float[] mpTexcoord = new float[eyelidVertexNum * 2];
            Marshal.Copy(mpMeshData[i].texcoord, mpTexcoord, 0, eyelidVertexNum * 2);

            int index = 0;
            for (int j = 0; j < mf.Length; j++)
            {
                Vector2[] uv = mf[j].mesh.uv;
                for (int k = 0; k < mf[j].mesh.vertexCount; k++)
                {
                    uv[k] = new Vector2(1.0F - mpTexcoord[index * 2 + 0], mpTexcoord[index * 2 + 1]);
                    index++;
                }
                mf[j].mesh.uv = uv;
            }
        }
        for (int i = 4; i < 5; i++)
        {
            MeshFilter[] mf = meshObj_[meshDataIndex[i]].GetComponentsInChildren<MeshFilter>();

            // convert IntPtr to float[]
            float[] mpTexcoord = new float[lowerTeethVertexNum * 2];
            Marshal.Copy(mpMeshData[i].texcoord, mpTexcoord, 0, lowerTeethVertexNum * 2);

            int index = 0;
            for (int j = 0; j < mf.Length; j++)
            {
                Vector2[] uv = mf[j].mesh.uv;
                for (int k = 0; k < mf[j].mesh.vertexCount; k++)
                {
                    uv[k] = new Vector2(1.0F - mpTexcoord[index * 2 + 0], mpTexcoord[index * 2 + 1]);
                    index++;
                }
                mf[j].mesh.uv = uv;
            }
        }
        for (int i = 5; i < 6; i++)
        {
            MeshFilter[] mf = meshObj_[meshDataIndex[i]].GetComponentsInChildren<MeshFilter>();

            // convert IntPtr to float[]
            float[] mpTexcoord = new float[upperTeethVertexNum * 2];
            Marshal.Copy(mpMeshData[i].texcoord, mpTexcoord, 0, upperTeethVertexNum * 2);

            int index = 0;
            for (int j = 0; j < mf.Length; j++)
            {
                Vector2[] uv = mf[j].mesh.uv;
                for (int k = 0; k < mf[j].mesh.vertexCount; k++)
                {
                    uv[k] = new Vector2(1.0F - mpTexcoord[index * 2 + 0], mpTexcoord[index * 2 + 1]);
                    index++;
                }
                mf[j].mesh.uv = uv;
            }
        }

        // update indices
        for (int i = 0; i < 2; i++)
        {
            MeshFilter[] mf = meshObj_[meshDataIndex[i]].GetComponentsInChildren<MeshFilter>();

            // convert IntPtr to ushort[]
            short[] mpIndices = new short[trianglesNum * 3];
            Marshal.Copy(mpMeshData[i].indices, mpIndices, 0, trianglesNum * 3);

            int[] meshTriangles = new int[trianglesNum * 3];
            for (int j = 0; j < trianglesNum * 3; j++)
            {
                meshTriangles[j] = mpIndices[j];
            }

            for (int j = 0; j < mf.Length; j++)
            {
                mf[j].mesh.triangles = meshTriangles;
            }
        }
        for (int i = 2; i < 4; i++)
        {
            MeshFilter[] mf = meshObj_[meshDataIndex[i]].GetComponentsInChildren<MeshFilter>();

            // convert IntPtr to ushort[]
            short[] mpIndices = new short[eyelidTrianglesNum * 3];
            Marshal.Copy(mpMeshData[i].indices, mpIndices, 0, eyelidTrianglesNum * 3);

            int[] meshTriangles = new int[eyelidTrianglesNum * 3];
            for (int j = 0; j < eyelidTrianglesNum * 3; j++)
            {
                meshTriangles[j] = mpIndices[j];
            }

            for (int j = 0; j < mf.Length; j++)
            {
                mf[j].mesh.triangles = meshTriangles;
            }
        }
        for (int i = 4; i < 5; i++)
        {
            MeshFilter[] mf = meshObj_[meshDataIndex[i]].GetComponentsInChildren<MeshFilter>();

            // convert IntPtr to ushort[]
            short[] mpIndices = new short[lowerTeethTrianglesNum * 3];
            Marshal.Copy(mpMeshData[i].indices, mpIndices, 0, lowerTeethTrianglesNum * 3);

            int[] meshTriangles = new int[lowerTeethTrianglesNum * 3];
            for (int j = 0; j < lowerTeethTrianglesNum * 3; j++)
            {
                meshTriangles[j] = mpIndices[j];
            }

            for (int j = 0; j < mf.Length; j++)
            {
                mf[j].mesh.triangles = meshTriangles;
            }
        }
        for (int i = 5; i < 6; i++)
        {
            MeshFilter[] mf = meshObj_[meshDataIndex[i]].GetComponentsInChildren<MeshFilter>();

            // convert IntPtr to ushort[]
            short[] mpIndices = new short[upperTeethTrianglesNum * 3];
            Marshal.Copy(mpMeshData[i].indices, mpIndices, 0, upperTeethTrianglesNum * 3);

            int[] meshTriangles = new int[upperTeethTrianglesNum * 3];
            for (int j = 0; j < upperTeethTrianglesNum * 3; j++)
            {
                meshTriangles[j] = mpIndices[j];
            }

            for (int j = 0; j < mf.Length; j++)
            {
                mf[j].mesh.triangles = meshTriangles;
            }
        }

        // free
        for (int i = 0; i < 6; i++)
        {
            handleMeshVertex[i].Free();
            handleMeshTexcoord[i].Free();
            handleMeshIndices[i].Free();
        }

        // Update Textures
        UpdateTextures();

        // update meshs
        UpdateModels();

        // face ready
        isFaceReady_ = true;
    }

    // Destroy face
    public void DestroyFace()
    {
        if (isFaceReady_)
        {
#if false
            // destroy cosme
            if (cosmeId_ != UIntPtr.Zero)
            {
                mpCosme_.Destroy(cosmeId_);
                cosmeId_ = UIntPtr.Zero;
            }
#endif

            // unload mpFace
            mpFace_.Unload();

            // Destroy Textures
            for (int i = 0; i < DESTROY_TEXTURES_RENDER_NUM; i++)
            {
                Renderer[] renderer = meshObj_[i].GetComponentsInChildren<Renderer>();
                for (int j = 0; j < renderer.Length; j++)
                {
                    renderer[j].material.mainTexture = null;
                }
            }
            GC.Collect();
            Resources.UnloadUnusedAssets();

            isFaceReady_ = false;
        }
    }

    public void ClearExpression()
    {
        exprId_ = 0;
        SetExpression(exprId_);
    }

    // Update Expression
    public void UpdateExpression()
    {
        // update expression ID
        exprId_++;
        if (exprId_ == 8)
        {
            exprId_ = 0;
        }

        // set new expression ID
        SetExpression(exprId_);
    }

    // Set Expression
    void SetExpression(int exprId)
    {
        if (isFaceReady_)
        {
            int offset = 10;
            float[] gain = new float[32];
            for (int i = 0; i < 32; i++)
            {
                gain[i] = (i == exprId + offset && exprId != 0) ? 1.0F : 0.0F;
            }
            int msec = 500;     // duration of animation in milli second
            mpAnimation_.Express(msec, gain, 1.0F);
        }
    }

#if false
    // Set Cosme
    // 0: OFF, 1~: CosmeIndex
    public void SetCosme(int cosmeListIndex)
    {
        if (isFaceReady_)
        {
            // destroy cosme
            if (cosmeId_ != UIntPtr.Zero)
            {
                mpCosme_.UnsetCosme(mpFace_);
                mpCosme_.Destroy(cosmeId_);
                cosmeId_ = UIntPtr.Zero;
            }

            // set cosme
            if (cosmeListIndex != 0)
            {
                int cosmeIndex = cosmeListIndex - 1;
                string cosmeFilePath = resourcePath_ + "/cosme/cosme" + cosmeIndex.ToString() + ".csm";
                cosmeId_ = mpCosme_.Create(cosmeFilePath);
                mpCosme_.SetCosme(mpFace_, cosmeId_);
            }

            // Destroy Front Textures
            for (int i = 0; i < 3; i++)
            {
                Renderer[] renderer = meshObj_[i].GetComponentsInChildren<Renderer>();
                for (int j = 0; j < renderer.Length; j++)
                {
                    renderer[j].material.mainTexture = null;
                }
            }
            GC.Collect();
            Resources.UnloadUnusedAssets();

            // Update Textures
            UpdateTextures();
        }
    }
#endif

    // Set Blink Enable
    public void SetBlinkEnable(bool isEnable)
    {
        isBlinkEnable_ = isEnable;
        if (!isBlinkEnable_)
        {
            unconsciousGain_ = 1.0F;
        }
        else
        {
            unconsciousGain_ = 0.0F;
        }
        if (isFaceReady_)
        {
            if (enableUnconscious_)
            {
                mpAnimation_.SetUnconsciousGain(unconsciousGain_);
            }
            else
            {
                mpAnimation_.SetUnconsciousGain(0.0F);
            }
        }
    }

    // Set Blink Gain
    public void SetBlinkGain(float gain)
    {
        blinkGainLeft_ = gain;
        blinkGainRight_ = gain;
    }

    // Set Voice
    void SetVoice(int voiceIndex)
    {
        if (isFaceReady_)
        {
            ClearVoice();
            if (voiceIndex == -1)
            {
                // Voice Off
                return;
            }

            // create voice
            CreateVoice(voiceIndex);
        }
    }

    // Create Voice
    void CreateVoice(int voiceIndex)
    {
        if (isFaceReady_)
        {
            // start lipsync
            string voiceFilePath = resourcePath_ + "/voice/voice" + voiceIndex.ToString() + ".wav";
            voiceId_ = mpSpeech_.CreateVoice(voiceFilePath);
            mpSpeech_.Speak(voiceId_);

            // play an audio file
            GameObject refObj = GameObject.Find("VoiceAudioSource");
            VoicePlayer voicePlayer = refObj.GetComponent<VoicePlayer>();
            voicePlayer.Play(voiceIndex);
        }
    }

    // Clear Voice
    public void ClearVoice()
    {
        if (isFaceReady_)
        {
            if (voiceId_ != UIntPtr.Zero)
            {
                // Player Stop
                GameObject refObj = GameObject.Find("VoiceAudioSource");
                VoicePlayer voicePlayer = refObj.GetComponent<VoicePlayer>();
                voicePlayer.Stop();

                // lipsync stop
                mpSpeech_.SpeakStop();
                mpSpeech_.DestroyVoice(voiceId_);
                voiceId_ = UIntPtr.Zero;
            }
        }
    }

    // update models
    void UpdateModels()
    {
        // create mesh buffer
        int eyelidVertexNum = mpMesh_.GetPartsMaxVertexNum(MpMesh.MeshParts.MESH_PARTS_RIGHT_EYELID);
        int lowerTeethVertexNum = mpMesh_.GetPartsMaxVertexNum(MpMesh.MeshParts.MESH_PARTS_LOWER_TEETH);
        int upperTeethVertexNum = mpMesh_.GetPartsMaxVertexNum(MpMesh.MeshParts.MESH_PARTS_UPPER_TEETH);

        int[] meshDataIndex = new int[6] { (int)MESH_DATA_TYPE.MESH_DATA_TYPE_FACE, (int)MESH_DATA_TYPE.MESH_DATA_TYPE_LIP, (int)MESH_DATA_TYPE.MESH_DATA_TYPE_RIGHT_EYELID, (int)MESH_DATA_TYPE.MESH_DATA_TYPE_LEFT_EYELID, (int)MESH_DATA_TYPE.MESH_DATA_TYPE_LOWER_TEETH, (int)MESH_DATA_TYPE.MESH_DATA_TYPE_UPPER_TEETH };
        MpMesh.MeshData[] mpMeshData = new MpMesh.MeshData[6];      // 0:Face, 1:Lip, 2:RightEyelid, 3:LeftEyelid, 4:LowerTeeth, 5:UpperTeeth
        float[][] meshVertex = new float[6][];
        float[][] meshTexcoord = new float[6][];
        GCHandle[] handleMeshVertex = new GCHandle[6];
        GCHandle[] handleMeshTexcoord = new GCHandle[6];
        for (int i = 0; i < 2; i++)
        {
            meshVertex[i] = new float[faceVertexNum_ * 3];
            meshTexcoord[i] = new float[faceVertexNum_ * 2];
        }
        for (int i = 2; i < 4; i++)
        {
            meshVertex[i] = new float[eyelidVertexNum * 3];
            meshTexcoord[i] = new float[eyelidVertexNum * 2];
        }
        for (int i = 4; i < 5; i++)
        {
            meshVertex[i] = new float[lowerTeethVertexNum * 3];
            meshTexcoord[i] = new float[lowerTeethVertexNum * 2];
        }
        for (int i = 5; i < 6; i++)
        {
            meshVertex[i] = new float[upperTeethVertexNum * 3];
            meshTexcoord[i] = new float[upperTeethVertexNum * 2];
        }
        for (int i = 0; i < 6; i++)
        {
            // convert float[] to IntPtr
            handleMeshVertex[i] = GCHandle.Alloc(meshVertex[i], GCHandleType.Pinned);
            handleMeshTexcoord[i] = GCHandle.Alloc(meshTexcoord[i], GCHandleType.Pinned);

            mpMeshData[i].vertexNum = faceVertexNum_;
            mpMeshData[i].vertex = handleMeshVertex[i].AddrOfPinnedObject();
            mpMeshData[i].normal = IntPtr.Zero;
            mpMeshData[i].texcoord = handleMeshTexcoord[i].AddrOfPinnedObject();
            mpMeshData[i].vertexFixed = IntPtr.Zero;
            mpMeshData[i].trianglesNum = 0;
            mpMeshData[i].indices = IntPtr.Zero;
        }

        // get mesh
        MpMesh.MeshConfig config;
        config.type = MpMesh.MeshType.MESH_TYPE_RELIEF;
        config.direction = MpMesh.MeshDirection.MESH_DIRECTION_Z_PLUS;
        config.offset.x = 0.0F;
        config.offset.y = 0.0F;
        config.offset.z = 0.0F;
        config.scale = 0.5F;
        config.depthScale = 0.5F;
        config.smoothMeshIterations = 0;
        mpMesh_.GetPartsMesh(MpMesh.MeshParts.MESH_PARTS_FACE, ref mpMeshData[0], ref config);
        mpMesh_.GetPartsMesh(MpMesh.MeshParts.MESH_PARTS_LIP, ref mpMeshData[1], ref config);
        mpMesh_.GetPartsMesh(MpMesh.MeshParts.MESH_PARTS_RIGHT_EYELID, ref mpMeshData[2], ref config);
        mpMesh_.GetPartsMesh(MpMesh.MeshParts.MESH_PARTS_LEFT_EYELID, ref mpMeshData[3], ref config);
        mpMesh_.GetPartsMesh(MpMesh.MeshParts.MESH_PARTS_LOWER_TEETH, ref mpMeshData[4], ref config);
        mpMesh_.GetPartsMesh(MpMesh.MeshParts.MESH_PARTS_UPPER_TEETH, ref mpMeshData[5], ref config);

        // update mesh
        for (int i = 0; i < 2; i++)
        {
            MeshFilter[] mf = meshObj_[meshDataIndex[i]].GetComponentsInChildren<MeshFilter>();

            // convert IntPtr to float[]
            float[] mpVertex = new float[faceVertexNum_ * 3];
            Marshal.Copy(mpMeshData[i].vertex, mpVertex, 0, faceVertexNum_ * 3);

            // update vertex
            int index = 0;
            for (int j = 0; j < mf.Length; j++)
            {
                Vector3[] vertices = mf[j].mesh.vertices;
                for (int k = 0; k < mf[j].mesh.vertexCount; k++)
                {
                    vertices[k] = new Vector3(mpVertex[index * 3 + 0], mpVertex[index * 3 + 1], -mpVertex[index * 3 + 2]);
                    index++;
                }
                mf[j].mesh.vertices = vertices;
            }
        }
        for (int i = 2; i < 4; i++)
        {
            MeshFilter[] mf = meshObj_[meshDataIndex[i]].GetComponentsInChildren<MeshFilter>();

            // convert IntPtr to float[]
            float[] mpVertex = new float[eyelidVertexNum * 3];
            Marshal.Copy(mpMeshData[i].vertex, mpVertex, 0, eyelidVertexNum * 3);

            // update vertex
            int index = 0;
            for (int j = 0; j < mf.Length; j++)
            {
                Vector3[] vertices = mf[j].mesh.vertices;
                for (int k = 0; k < mf[j].mesh.vertexCount; k++)
                {
                    vertices[k] = new Vector3(mpVertex[index * 3 + 0], mpVertex[index * 3 + 1], -mpVertex[index * 3 + 2]);
                    index++;
                }
                mf[j].mesh.vertices = vertices;
            }
        }
        for (int i = 4; i < 5; i++)
        {
            MeshFilter[] mf = meshObj_[meshDataIndex[i]].GetComponentsInChildren<MeshFilter>();

            // convert IntPtr to float[]
            float[] mpVertex = new float[lowerTeethVertexNum * 3];
            Marshal.Copy(mpMeshData[i].vertex, mpVertex, 0, lowerTeethVertexNum * 3);

            // update vertex
            int index = 0;
            for (int j = 0; j < mf.Length; j++)
            {
                Vector3[] vertices = mf[j].mesh.vertices;
                for (int k = 0; k < mf[j].mesh.vertexCount; k++)
                {
                    vertices[k] = new Vector3(mpVertex[index * 3 + 0], mpVertex[index * 3 + 1], -mpVertex[index * 3 + 2]);
                    index++;
                }
                mf[j].mesh.vertices = vertices;
            }
        }
        for (int i = 5; i < 6; i++)
        {
            MeshFilter[] mf = meshObj_[meshDataIndex[i]].GetComponentsInChildren<MeshFilter>();

            // convert IntPtr to float[]
            float[] mpVertex = new float[upperTeethVertexNum * 3];
            Marshal.Copy(mpMeshData[i].vertex, mpVertex, 0, upperTeethVertexNum * 3);

            // update vertex
            int index = 0;
            for (int j = 0; j < mf.Length; j++)
            {
                Vector3[] vertices = mf[j].mesh.vertices;
                for (int k = 0; k < mf[j].mesh.vertexCount; k++)
                {
                    vertices[k] = new Vector3(mpVertex[index * 3 + 0], mpVertex[index * 3 + 1], -mpVertex[index * 3 + 2]);
                    index++;
                }
                mf[j].mesh.vertices = vertices;
            }
        }

        // free
        for (int i = 0; i < 6; i++)
        {
            handleMeshVertex[i].Free();
            handleMeshTexcoord[i].Free();
        }
    }

    // Update Textures
    void UpdateTextures()
    {
        int[] meshDataIndex = new int[6] { (int)MESH_DATA_TYPE.MESH_DATA_TYPE_FACE, (int)MESH_DATA_TYPE.MESH_DATA_TYPE_LIP, (int)MESH_DATA_TYPE.MESH_DATA_TYPE_RIGHT_EYELID, (int)MESH_DATA_TYPE.MESH_DATA_TYPE_LEFT_EYELID, (int)MESH_DATA_TYPE.MESH_DATA_TYPE_LOWER_TEETH, (int)MESH_DATA_TYPE.MESH_DATA_TYPE_UPPER_TEETH };

        // create texture
        Texture2D[] mpTexture = new Texture2D[6];
        for (int i = 0; i < 6; i++)
        {
            // get image
            MpMesh.ImageData mpImageData = new();
            mpMesh_.GetPartsImage((MpMesh.MeshParts)i, ref mpImageData);

            int w = mpImageData.width;
            int h = mpImageData.height;

            // convert IntPtr to byte[]
            byte[] mpRGBA = new byte[w * h * 4];    // bottom left origin
            Marshal.Copy(mpImageData.rgba, mpRGBA, 0, w * h * 4);

            mpTexture[i] = new Texture2D(w, h); // bottom right origin
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    byte r = mpRGBA[(y * w + (w - 1 - x)) * 4 + 0];
                    byte g = mpRGBA[(y * w + (w - 1 - x)) * 4 + 1];
                    byte b = mpRGBA[(y * w + (w - 1 - x)) * 4 + 2];
                    byte a = mpRGBA[(y * w + (w - 1 - x)) * 4 + 3];
                    Color32 c = new Color32(r, g, b, a);
                    mpTexture[i].SetPixel(x, y, c);
                }
            }
            mpTexture[i].wrapMode = TextureWrapMode.Clamp;
            mpTexture[i].Apply();
        }

        // update texture
        for (int i = 0; i < 6; i++)
        {
            Renderer[] renderer = meshObj_[meshDataIndex[i]].GetComponentsInChildren<Renderer>();

            for (int j = 0; j < renderer.Length; j++)
            {
                renderer[j].material.mainTexture = mpTexture[i];
                renderer[j].material.color = Color.white;
                //renderer [j].material.SetTexture ("_EmissionMap", mpTexture[i]);
            }
        }

        // create background texture
        Texture2D faceTex = mpTexture[0];
        Texture2D bgTex = new(faceTex.width, faceTex.height);
        Color[] bgColorArray = faceTex.GetPixels();
        for (int y = 0; y < bgTex.height; ++y)
        {
            for (int x = 0; x < bgTex.width; ++x)
            {
                Color color = bgColorArray[y * bgTex.width + x];
                color.a = 1.0f;
                bgTex.SetPixel(x, bgTex.height - 1 - y, color);
            }
        }
        bgTex.Apply();

        // set to background mesh
        Renderer[] bgRenderer = meshObj_[(int)MESH_DATA_TYPE.MESH_DATA_TYPE_BACKGROUND].GetComponentsInChildren<Renderer>();
        for (int i = 0; i < bgRenderer.Length; i++)
        {
            bgRenderer[i].material.mainTexture = bgTex;
            bgRenderer[i].material.color = Color.white;
        }
    }

    // get msec
    int Getmsec()
    {
        System.DateTime now = System.DateTime.Now;
        double msec = (now - startTime_).TotalMilliseconds;
        return (int)msec;
    }

    byte[] ReadBytes(string path)
    {
        FileStream fs = new(path, FileMode.Open);
        BinaryReader bin = new(fs);
        byte[] result = bin.ReadBytes((int)bin.BaseStream.Length);
        bin.Close();
        return result;
    }

    Texture2D LoadTexture(string texturePath)
    {
        byte[] textureBytes = ReadBytes(texturePath);
        Texture2D tex = new(0, 0);
        tex.LoadImage(textureBytes);
        return tex;
    }

    IntPtr ToRgbaIntPtr(Texture2D texture, bool flip)
    {
        // get byte data
        int bpp = 4;
        int textureWidth = texture.width;
        int textureHeight = texture.height;
        Color[] colorArray = texture.GetPixels();
        byte[] byteArray = new byte[bpp * textureWidth * textureHeight];
        for (int y = 0; y < textureHeight; ++y)
        {
            for (int x = 0; x < textureWidth; ++x)
            {
                Color cl = colorArray[y * textureWidth + x];
                int pixelIndex = flip ? (textureHeight - 1 - y) * textureWidth + x : y * textureWidth + x;
                byteArray[bpp * pixelIndex + 0] = (byte)(cl.r * 255);
                byteArray[bpp * pixelIndex + 1] = (byte)(cl.g * 255);
                byteArray[bpp * pixelIndex + 2] = (byte)(cl.b * 255);
                byteArray[bpp * pixelIndex + 3] = (byte)(cl.a * 255);
            }
        }
        IntPtr rgbaImage = Marshal.AllocCoTaskMem(
            Marshal.SizeOf(typeof(byte)) * byteArray.Length);
        Marshal.Copy(byteArray, 0, rgbaImage, byteArray.Length);
        return rgbaImage;
    }

    MpSynth.Img ToSynthImage(Texture2D texture)
    {
        // get byte data
        int bpp = 3;
        int textureWidth = texture.width;
        int textureHeight = texture.height;
        Color[] colorArray = texture.GetPixels();
        byte[] byteArray = new byte[bpp * textureWidth * textureHeight];
        for (int y = 0; y < textureHeight; ++y)
        {
            for (int x = 0; x < textureWidth; ++x)
            {
                Color cl = colorArray[(textureHeight - 1 - y) * textureWidth + x];
                int pixelIndex = y * textureWidth + x;
                byteArray[bpp * pixelIndex + 0] = (byte)(cl.r * 255);
                byteArray[bpp * pixelIndex + 1] = (byte)(cl.g * 255);
                byteArray[bpp * pixelIndex + 2] = (byte)(cl.b * 255);
            }
        }
        IntPtr rgbImage = Marshal.AllocCoTaskMem(
            Marshal.SizeOf(typeof(byte)) * byteArray.Length);
        Marshal.Copy(byteArray, 0, rgbImage, byteArray.Length);
        MpSynth.Img img = new(textureWidth, textureHeight, rgbImage, IntPtr.Zero);
        return img;
    }

    MpSynth.Tex ToSynthTexture(Texture2D texture)
    {
        IntPtr rgbaImage = ToRgbaIntPtr(texture, true);
        MpSynth.Tex tex = new(texture.width, texture.height, rgbaImage);
        return tex;
    }
    MpaAnalyzer.MpaImage ToAnalyzerImage(Texture2D texture)
    {
        IntPtr rgbaImage = ToRgbaIntPtr(texture, false);
        MpaAnalyzer.MpaImage img = new(rgbaImage, texture.width, texture.height, 4);
        return img;
    }

    public int StartEditingFP()
    {
        fpEditing_ = true;
        SetMeshActive(false);
        return 0;
    }

    public int EndEditingFP()
    {
        fpEditing_ = false;
        SetMeshActive(true);
        return 0;
    }

    public void SetMeshActive(bool isActive)
    {
        for (int i = 0; i < meshObj_.Length; ++i)
        {
            meshObj_[i].SetActive(isActive);
        }
    }

    // create an avatar from given image and feature points
    public int CreateAvatar(Texture2D texture, MpSynth.MpFeaturePoints mpFPPoints)
    {
        // get byte data of input image
        MpSynth.Img inputImage = ToSynthImage(texture);

        // load mouth images
        MpSynth.Tex lowerTeethImg = new();
        MpSynth.Tex upperTeethImg = new();
        if (replaceMouthTexture_)
        {
            // get teeth textures
            string folder = resourcePath_ + "/mouth/";
            string lowerTeethTexPath = folder + "lower_teeth.png";
            Texture2D lowerTeethTexture = LoadTexture(lowerTeethTexPath);
            string upperTeethTexPath = folder + "upper_teeth.png";
            Texture2D upperTeethTexture = LoadTexture(upperTeethTexPath);
            lowerTeethImg = ToSynthTexture(lowerTeethTexture);
            upperTeethImg = ToSynthTexture(upperTeethTexture);
        }

        // synthesize the image and create a face bin file
        string faceBinPath = resourcePath_ + "/face/face.bin";
        int ret = mpSynth_.Synth(inputImage, faceBinPath, mpFPPoints, lowerTeethImg, upperTeethImg);
        if (ret == 0)
        {
            // replace avatar
            LoadFaceData(faceBinPath);

            return 0;
        }

        return -1;
    }

    public void StartLipSync()
    {
        SetVoice((int)voiceName_);
    }

    public void EnableUnconscious(bool b)
    {
        enableUnconscious_ = b;
    }

    public void ReplaceMouthTexture(bool b)
    {
        replaceMouthTexture_ = b;
    }

    public void EnableBackground(bool b)
    {
        enableBackground_ = b;
    }
}
