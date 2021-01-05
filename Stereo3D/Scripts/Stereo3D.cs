
// Stereoscopic 3D system by Vital Volkov
// Usage:
// 1) The main parameter for the correct size of `User IPD`(Interpupillary distance) in millimeters is `PPI`(Pixels Per Inch) or `Pixel Pitch`(distance between pixel centers) of the screen. Required for precision to calculate screen width.
// The system will try to autodetect `PPI` of the screen (In my case PPI = 96 on 23 inch LG D2342P monitor). If correct `PPI` failed autodetected from the screen then find one of two in Tech Specs of your screen and set it manually.
// 2) If PPI or Pixel Pitch is set correctly then "User IPD" will have real distance in millimeters and for precision realistic Stereo3D vision, it must be set the same as user real IPD.
// If you don't know your IPD you can measure it with a mirror and ruler - put a ruler on the mirror in front of your face. Close right eye and move such that left eye pupillary look at themself through Zero mark on a scale of Ruler, at this moment, is important to stay still, close the left eye and open the right eye, and look at the right eye pupillary in the mirror through the Ruler scale and you will see your IPD in millimeters. 
// 3) Select the Stereo 3D Method. Set your real `User IPD` in the Stereo 3D system and go. If you don't see Stereo 3D then toggle `Swap Left-Right Cameras`. If you want to see virtual reality in a different size feel then uncheck the `Match User IPD` mark and set `Virtual IPD` larger than `User IPD` for toy world and vise versa.
// 4) `Screen Distance` shows the distance between eyes and screen where real FOV(Field Of View) will match the virtual FOV. So, measure the distance from your eyes position to screen, tune FOV till `Screen Distance` matches the measured one and you get the most realistic view.
// Tested on Unity 2019 and 2020 with default render + Post Processing Stack v2, URP, and HDRP.
// Enjoy.

using UnityEngine;
using UnityEngine.Rendering;
using System.Runtime.InteropServices;

public class Stereo3D : MonoBehaviour
{
    public enum Method {Interleaved, SideBySide, OverUnder, Anaglyph}; 
    public enum InterleavedType {Horizontal, Vertical, Checkerboard};
    public enum ParentCam {Left, Center, Right};

    [Header("Settings")]
    public bool S3DEnabled = true; //mono or Stereo3D enabled
    public bool swapLR; //swap left-right cameras
    public float userIPD = 66; //important setting in mm for correct Stereo3D. User should set his REAL IPD(Interpupillary Distance) and REAL screen size via PPI or pixelPitch for real millimeters match
    public float virtualIPD = 66; //set virtual IPD not match real and see world in different size feel
    public bool matchUserIPD = true; //set virtual IPD match to User IPD for realistic view or unset for unlock and let different settings for real and virtual IPD's
    public float PPI = 96; //how many Pixels Per Inch screen have for correct real screen size calculation(see tech specs for screen and set PPI or pixelPitch)
    public float pixelPitch = .265f; //distance between pixels centers in mm. If no PPI then pixelPitch must be in tech specs for screen
    public float hFOV = 90; //horizontal Field Of View
    public bool GuiVisible = true; //GUI window visible or not on start
    public KeyCode GuiKey = KeyCode.Tab; //GUI window show hide Key
    public KeyCode S3DKey = KeyCode.KeypadMultiply; //S3D enable disable shortcut Key 
    public KeyCode increaseFovKey = KeyCode.KeypadMinus; //increase Field Of View shortcut Key + hold "Shift" Key for faster change
    public KeyCode decreaseFovKey = KeyCode.KeypadPlus; //decrease Field Of View shortcut Key + hold "Shift" Key for faster change
    public Method method = Method.Interleaved; //Stereo3D output method
    public InterleavedType interleavedType = InterleavedType.Horizontal; //Type of Interleaved Stereo3D output method
    public ParentCam parentCam = ParentCam.Center; //for which of eye parent camera renders: left, right or center-symmetric(important for sight aiming in VR)
    public Color anaglyphLeftColor = Color.red; //tweak colors at runtime to best match different goggles
    public Color anaglyphRightColor = Color.cyan;
    public GameObject cameraPrefab; //if empty, Stereo3D cameras is copy of main cam. Set prefab if need custom settings &/or components
    public RenderTextureFormat RTFormat = RenderTextureFormat.DefaultHDR; //DefaultHDR(16bitFloat) be able to contain Post Process Effects and give fps gain from 328 to 343. In my case RGB111110Float is fastest - 346fps.
    public bool shiftMatrixOrLens = true; //shift image Vanish points to User IPD directly via camera Matrix(fps gain) or via camera's "physically" settings "lensShift"(required for Post Processing Stack V2 pack as it resets matrix and yields incorrect aspect)

    [Header("Info")]
    public Material S3DMaterial; //generated material

    Camera cam;
    Camera leftCam;
    Camera rightCam;
    int cullingMask;
    float nearClip;
    vertex[] vertices = new vertex[4];
    bool mainCam;
    bool defaultRender;

    struct vertex
    {
        public Vector2 pos;
        public Vector2 uv;
    }

    bool lastS3DEnabled;
    bool lastSwapLR;
    float lastUserIPD;
    float lastVirtualIPD;
    bool lastMatchUserIPD;
    float lastPPI;
    float lastPixelPitch;
    float lastHFOV;
    Method lastMethod;
    InterleavedType lastInterleavedType;
    Color lastAnaglyphLeftColor;
    Color lastAnaglyphRightColor;
    Rect lastCamRect;

    void OnEnable()
    {
        if (GraphicsSettings.currentRenderPipeline == null)
            defaultRender = true;

        S3DMaterial = new Material(Shader.Find("Stereo3D Screen Quad"));
		S3DMaterial.SetColor("_LeftCol", anaglyphLeftColor);
		S3DMaterial.SetColor("_RightCol", anaglyphRightColor);

	    cam = GetComponent<Camera>();

        if (cam == Camera.main)
            mainCam = true;

        cullingMask = cam.cullingMask;
        nearClip = cam.nearClipPlane;

        if (cameraPrefab)
        {
	        leftCam = Instantiate(cameraPrefab, transform.position, transform.rotation).GetComponent<Camera>();
            leftCam.name = "leftCam";
	        rightCam = Instantiate(cameraPrefab, transform.position, transform.rotation).GetComponent<Camera>();
            rightCam.name = "rightCam";
        }
        else
        {
	        leftCam = new GameObject("leftCam").AddComponent<Camera>();
	        rightCam = new GameObject("rightCam").AddComponent<Camera>();
	        leftCam.CopyFrom(cam);
	        rightCam.CopyFrom (cam);
            leftCam.rect = rightCam.rect = Rect.MinMaxRect(0, 0, 1, 1);
        }
	
	    leftCam.transform.parent = rightCam.transform.parent = transform;
		
		if (Screen.dpi != 0)
			PPI = Screen.dpi;

        PPISet();
        UserIPDSet();
        VirtualIPDSet();
        HFOVSet();
        CamSet();
	    RTSet();

        lastS3DEnabled = S3DEnabled;
        lastSwapLR = swapLR;
        lastUserIPD = userIPD;
        lastVirtualIPD = virtualIPD;
        lastMatchUserIPD = matchUserIPD;
        lastPPI = PPI;
        lastPixelPitch = pixelPitch;
        lastHFOV = hFOV;
        lastMethod = method;
        lastInterleavedType = interleavedType;
        lastAnaglyphLeftColor = anaglyphLeftColor;
        lastAnaglyphRightColor = anaglyphRightColor;
        lastCamRect = cam.rect;
    }

    float vFOV;

    void Update()
    {
        if (lastS3DEnabled != S3DEnabled)
        {
            lastS3DEnabled = S3DEnabled;
            RTSet();
        }

        if (lastSwapLR != swapLR)
        {
            lastSwapLR = swapLR;
            CamSet();
        }

        if (lastUserIPD != userIPD)
        {
            lastUserIPD = userIPD;
            UserIPDSet();
        }

        if (lastVirtualIPD != virtualIPD)
        {
            lastVirtualIPD = virtualIPD;
            VirtualIPDSet();
        }

        if (lastMatchUserIPD != matchUserIPD)
        {
            lastMatchUserIPD = matchUserIPD;
            VirtualIPDSet();
        }

        if (lastPPI != PPI)
        {
            lastPPI = PPI;
            PPISet();
        }

        if (lastPixelPitch != pixelPitch)
        {
            lastPixelPitch = pixelPitch;
            PixelPitchSet();
        }

        if (cam.fieldOfView != vFOV) //check camera FOV changes to set FOV from other scripts
		    hFOV = Mathf.Atan(cam.aspect * Mathf.Tan(cam.fieldOfView * Mathf.PI / 360)) * 360 / Mathf.PI;

        if (lastHFOV != hFOV)
        {
            lastHFOV = hFOV;
            HFOVSet();
        }

        if (lastMethod != method)
        {
            lastMethod = method;
            ViewSet();
            RTSet();
        }

        if (lastInterleavedType != interleavedType)
        {
            lastInterleavedType = interleavedType;
            RTSet();
        }

        if (lastAnaglyphLeftColor != anaglyphLeftColor)
        {
            lastAnaglyphLeftColor = anaglyphLeftColor;
		    S3DMaterial.SetColor("_LeftCol", anaglyphLeftColor);
        }

        if (lastAnaglyphRightColor != anaglyphRightColor)
        {
            lastAnaglyphRightColor = anaglyphRightColor;
		    S3DMaterial.SetColor("_RightCol", anaglyphRightColor);
        }

        if (lastCamRect != cam.rect)
        {
            lastCamRect = cam.rect;
            ViewSet();
            RTSet();
        }

        if (mainCam) //exclude GUI and Key controls from secondary cameras
        {
            if (Input.GetKeyUp(GuiKey))
                GuiVisible = !GuiVisible;

            if (Input.GetKeyDown(S3DKey))
            {
                S3DEnabled = !S3DEnabled;
                RTSet();
            }

            if (Input.GetKey(increaseFovKey) && !Input.GetKey(KeyCode.LeftControl))
            {
                if (Input.GetKey(KeyCode.LeftShift))
                    hFOV += 1;
                else
                    hFOV += .1f;

                HFOVSet();
            }

            if (Input.GetKey(decreaseFovKey) && !Input.GetKey(KeyCode.LeftControl))
            {
                if (Input.GetKey(KeyCode.LeftShift))
                    hFOV -= 1;
                else
                    hFOV -= .1f;

                HFOVSet();
            }

            if (Input.GetKey(decreaseFovKey) && Input.GetKey(KeyCode.LeftControl))
            {
                if (Input.GetKey(KeyCode.LeftShift))
                    virtualIPD += 10;
                else
                    virtualIPD += 1;

                VirtualIPDSet();
            }

            if (Input.GetKey(increaseFovKey) && Input.GetKey(KeyCode.LeftControl))
            {
                if (Input.GetKey(KeyCode.LeftShift))
                    virtualIPD -= 10;
                else
                    virtualIPD -= 1;

                VirtualIPDSet();
            }
        }
    }

    void PPISet()
    {
		PPI = Mathf.Max(PPI, 1);
        pixelPitch = 25.4f / PPI;

        ViewSet();
    }

    void PixelPitchSet()
    {
		pixelPitch = Mathf.Max(pixelPitch, .001f);
        PPI = 25.4f / pixelPitch;

        ViewSet();
    }

    void UserIPDSet()
    {
        userIPD = Mathf.Max(userIPD, 0);

        if (matchUserIPD)
            VirtualIPDSet();
        else
            CamSet();
    }

    void VirtualIPDSet()
    {
		if (matchUserIPD)
			virtualIPD = userIPD;
        else
            virtualIPD = Mathf.Max(virtualIPD, 0);

        CamSet();
    }

    void HFOVSet()
    {
        hFOV = Mathf.Clamp(hFOV, .1f, 179.9f);

        ViewSet();
    }

    void CamSet()
    {	
        if (parentCam == ParentCam.Left)
        {
			leftCam.transform.localPosition = Vector3.left * virtualIPD * .001f;
			rightCam.transform.localPosition = Vector3.zero;
		}
        else 
            if (parentCam == ParentCam.Right)
            {
			    leftCam.transform.localPosition = Vector3.zero;
			    rightCam.transform.localPosition = Vector3.right * virtualIPD * .001f;
		    }
            else
            {
			    leftCam.transform.localPosition = Vector3.left * virtualIPD * .0005f;
			    rightCam.transform.localPosition = Vector3.right * virtualIPD * .0005f;
		    }

        if (swapLR)
        {
            leftCam.transform.localPosition *= -1;
            rightCam.transform.localPosition *= -1;
        }

        ViewSet();
    }

    float scaleX;
    float scaleY;
    float screenDistance;

    void ViewSet()
    {
        float imageWidth = cam.pixelWidth * pixelPitch; //real size of rendered image on screen
        float aspect = cam.aspect;

        float shift = userIPD / imageWidth; //shift optic axis relative to the screen size (UserIPD/screenSize)
        scaleX = 1 / Mathf.Tan(hFOV * Mathf.PI / 360);
		scaleY = scaleX * aspect;
		vFOV = 360 * Mathf.Atan(1 / scaleY) / Mathf.PI;

        screenDistance = scaleX * imageWidth * .5f; //calculated distance to screen from user eyes where real FOV will match to virtual for realistic view

        if (shiftMatrixOrLens)
        {
            //set "shift" via matrix give fps gain from 304 to 308
            if (swapLR)
            {
		        leftCam.projectionMatrix = MatrixSet(leftCam.projectionMatrix, -shift);
		        rightCam.projectionMatrix = MatrixSet(rightCam.projectionMatrix, shift);
            }
            else
            {
		        leftCam.projectionMatrix = MatrixSet(leftCam.projectionMatrix, shift);
		        rightCam.projectionMatrix = MatrixSet(rightCam.projectionMatrix, -shift);
            }
        }
        else
        {
            //return matrix control to camera settings
            leftCam.ResetProjectionMatrix();
            rightCam.ResetProjectionMatrix();

            //set "shift" via cam "lensShift" required set "physical" cam
            leftCam.usePhysicalProperties = rightCam.usePhysicalProperties = true; //need set again after ResetProjectionMatrix
            cam.sensorSize = leftCam.sensorSize = rightCam.sensorSize = new Vector2(imageWidth, imageWidth / aspect);
            cam.gateFit = leftCam.gateFit = rightCam.gateFit = Camera.GateFitMode.None;
            Vector2 lensShift = new Vector2(-shift * .5f, 0);

            if (swapLR)
            {
                leftCam.lensShift = lensShift;
		        rightCam.lensShift = -lensShift;
            }
            else
            {
                leftCam.lensShift = -lensShift;
		        rightCam.lensShift = lensShift;
            }
        }

        leftCam.fieldOfView = rightCam.fieldOfView = cam.fieldOfView = vFOV;
    }

    Matrix4x4 MatrixSet(Matrix4x4 matrix, float signedShift)
    {	
        matrix[0, 0] = scaleX; // 1/tangent of half horizontal FOV
        matrix[0, 2] = signedShift; //shift whole image projection in X axis of screen clip space
        matrix[1, 1] = scaleY; //1/tangent of half vertical FOV

        return matrix;
    }

    RenderTexture leftCamRT;   
    RenderTexture rightCamRT;
    int pass;
    ComputeBuffer verticesBuffer;

    void RTSet()
    {
        ReleaseRT(); //remove RT before adding new to avoid duplication

	    if (S3DEnabled)
        {
		    cam.cullingMask = 0;
		    leftCam.enabled = true;	
		    rightCam.enabled = true;	
		    int rtWidth = cam.pixelWidth;
		    int rtHeight = cam.pixelHeight;
            Vertices();

            switch (method)
            {
                case Method.Interleaved:

                    int columns = 1;
                    int rows = 1;

                   switch (interleavedType)
                    {
		                case InterleavedType.Horizontal:
   		                    rows = rtHeight;
			                rtHeight /= 2; //optimize render as half of the rows not using per eye
                            pass = 0;
	   	                break;

   		                case InterleavedType.Vertical:
   		                    columns = rtWidth;
			                rtWidth /= 2; //optimize render as half of the columns not using per eye
                            pass = 1;
	   	                break;

   		                case InterleavedType.Checkerboard:
   		                    columns = rtWidth;
   		                    rows = rtHeight;
                            pass = 2;
	   	                break;
  	                }

		            S3DMaterial.SetInt("_Columns", columns);
		            S3DMaterial.SetInt("_Rows", rows);
                break;

                case Method.SideBySide:
				    rtWidth /= 2; //optimize render as image per eye squeeze in half to fit screen
                    vertices[2].uv = new Vector2(2, 1);
                    vertices[3].uv = new Vector2(2, 0);
                    pass = 3;
                break;

                case Method.OverUnder:
				    rtHeight /= 2; //optimize render as image per eye squeeze in half to fit screen
                    vertices[1].uv = new Vector2(0, 2);
                    vertices[2].uv = new Vector2(1, 2);
                    pass = 4;
                break;

                case Method.Anaglyph:
                    pass = 5;
                break;
            }

	        leftCamRT = new RenderTexture(rtWidth, rtHeight, 24, RTFormat);
	        rightCamRT = new RenderTexture(rtWidth, rtHeight, 24, RTFormat);
            leftCamRT.filterMode = FilterMode.Point;
            rightCamRT.filterMode = FilterMode.Point;

            leftCam.targetTexture = leftCamRT;
            rightCam.targetTexture = rightCamRT;

            S3DMaterial.SetTexture("_LeftTex", leftCamRT);
	        S3DMaterial.SetTexture("_RightTex", rightCamRT);

            verticesBuffer = new ComputeBuffer(4, Marshal.SizeOf(typeof(vertex)));
            verticesBuffer.SetData(vertices);
            S3DMaterial.SetBuffer("buffer", verticesBuffer);

            if (!defaultRender)
            {
                RenderPipelineManager.endCameraRendering += RenderQuad; //add render context
                cam.nearClipPlane = -1; //Hack for more fps in SRP(Scriptable Render Pipeline)
            }

            if (!Application.isEditor)
                cam.projectionMatrix = Matrix4x4.zero; //give fps gain from 308 to 328
        }
    }

    CommandBuffer commandBuffer;

    void RenderQuad(ScriptableRenderContext context, Camera camera) //render context for SRP
    {
        if (camera == cam)
        {
            commandBuffer = new CommandBuffer();
            commandBuffer.name = "screenQuad";

            //render clip space screen quad using S3DMaterial preset vertices buffer with:
            //commandBuffer.DrawProcedural(Matrix4x4.identity, S3DMaterial, pass, MeshTopology.Quads, 4); //this (need "nearClipPlane = -1" for same quad position as using Blit with custom camera Rect coordinates
            commandBuffer.Blit(null, cam.activeTexture, S3DMaterial, pass); //or this

            context.ExecuteCommandBuffer(commandBuffer);
            commandBuffer.Release();
            context.Submit();
        }
    }

    void Vertices() //set clip space vertices and texture coordinates for render fullscreen quad via shader buffer
    {
        if (defaultRender)
        {
            vertices[0].pos = new Vector2(-1, -1);
            vertices[1].pos = new Vector2(-1, 1);
            vertices[2].pos = new Vector2(1, 1);
            vertices[3].pos = new Vector2(1, -1);
        }
        else
        {
            //translate camera rect coordinates to clip coordinates
            //need when using custom camera rect with Blit or DrawProcedural & "nearClipPlane = -1" hack
            Vector4 clipRect = new Vector4(cam.rect.min.x, cam.rect.min.y, cam.rect.max.x, cam.rect.max.y) * 2 - Vector4.one;

            vertices[0].pos = new Vector2(clipRect.x, clipRect.y);
            vertices[1].pos = new Vector2(clipRect.x, clipRect.w);
            vertices[2].pos = new Vector2(clipRect.z, clipRect.w);
            vertices[3].pos = new Vector2(clipRect.z, clipRect.y);
        }

            vertices[0].uv = new Vector2(0, 0);
            vertices[1].uv = new Vector2(0, 1);
            vertices[2].uv = new Vector2(1, 1);
            vertices[3].uv = new Vector2(1, 0);
    }

    void OnDisable()
    {
        ReleaseRT();
        Destroy(leftCam.gameObject);
        Destroy(rightCam.gameObject);
        Destroy(S3DMaterial);
        Resources.UnloadUnusedAssets(); //free memory
    }

    void ReleaseRT()
    {
        if (!defaultRender)
            RenderPipelineManager.endCameraRendering -= RenderQuad; //remove render context

        leftCam.targetTexture = null;
        rightCam.targetTexture = null;
		leftCam.enabled = false;
		rightCam.enabled = false;
		cam.cullingMask = cullingMask;
        cam.nearClipPlane = nearClip;
        cam.ResetProjectionMatrix();

        if (verticesBuffer != null)
        {
            verticesBuffer.Release();
	        leftCamRT.Release();
	        rightCamRT.Release();
        }
    }

    //ignored in SRP(URP or HDRP) but in default render via cam buffer even empty function give fps gain from 294 to 308
    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        //if (defaultRender) //commented till SRP don't go here
        if (S3DEnabled)
            Graphics.Blit(null, destination, S3DMaterial, pass);
        else
            Graphics.Blit(source, destination);
    }

    //Immediate GUI Stereo3D settings panel (remove next three functions to delete IMGUI)
    Rect guiWindow = new Rect(20, 20, 640, 290);

    void OnGUI()
    {
        if (mainCam)
   	        if (GuiVisible)
            {
   		        Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

    	        guiWindow = GUILayout.Window(0, guiWindow, GuiWindowContent, "Stereo3D Settings");

		        if (!guiWindow.Contains(Event.current.mousePosition) && !Input.GetMouseButton(0))
			        GUI.UnfocusWindow();
	        }
            else
            {
   		        Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
	        }
    }

    void GuiWindowContent (int windowID)
    {
	    GUILayout.BeginHorizontal();
		    GUILayout.BeginVertical();
			    GUILayout.BeginHorizontal();
			    GUILayout.EndHorizontal();

		            S3DEnabled = GUILayout.Toggle(S3DEnabled, " Enable S3D");

                    string[] modeStrings = { "Interleaved", "Side By Side", "Over Under", "Anaglyph" };
				    method = (Method)GUILayout.SelectionGrid((int)method, modeStrings, 1, GUILayout.Width(100));

            GUILayout.EndVertical();
		    GUILayout.BeginVertical();
			    GUILayout.BeginHorizontal();

		            swapLR = GUILayout.Toggle(swapLR, " Swap Left-Right Cameras");

			    GUILayout.EndHorizontal();
			    GUILayout.BeginHorizontal();

                    string[] interleavedTypeStrings = { "Horizontal", "Vertical", "Checkerboard" };
				    interleavedType = (InterleavedType)GUILayout.Toolbar((int)interleavedType, interleavedTypeStrings, GUILayout.Width(298));

				    GUILayout.FlexibleSpace();
				    GUILayout.Label ("PPI", GUILayout.Width(30));
				
				    if (GUILayout.Button("-", GUILayout.Width(20)))
					    PPI -= 1;

                    string PPIString = StringCheck(PPI.ToString());
                    string fieldString = GUILayout.TextField(PPIString, 5, GUILayout.Width(40));

				    if (fieldString != PPIString)
                        PPI = System.Convert.ToSingle(fieldString);

				    if (GUILayout.Button("+", GUILayout.Width(20)))
					    PPI += 1;

				    GUILayout.Label (" pix");
				    GUILayout.Space(20);

			    GUILayout.EndHorizontal();
			    GUILayout.BeginHorizontal();

				    GUILayout.FlexibleSpace();
				    GUILayout.Label ("Pixel pitch", GUILayout.Width(70));

				    if (GUILayout.Button("-", GUILayout.Width(20)))
					    pixelPitch -= .001f;

                    string pixelPitchString = StringCheck(pixelPitch.ToString());
				    fieldString = GUILayout.TextField(pixelPitchString, 5, GUILayout.Width(40));

				    if (fieldString != pixelPitchString)
					    pixelPitch = System.Convert.ToSingle(fieldString);


				    if (GUILayout.Button("+", GUILayout.Width(20)))
					    pixelPitch += .001f;

				    GUILayout.Label (" mm");
				    GUILayout.Space(15);

			    GUILayout.EndHorizontal();
		    GUILayout.EndVertical();
	    GUILayout.EndHorizontal();
	    GUILayout.BeginHorizontal();
		    GUILayout.Space(4);
		    GUILayout.BeginVertical();

			    GUILayout.Label ("          User IPD", GUILayout.Width(100));
			    GUILayout.Label ("        Virtual IPD", GUILayout.Width(100));
			    GUILayout.Label ("  Horizontal FOV", GUILayout.Width(100));
			    GUILayout.Label ("     Vertical FOV", GUILayout.Width(100));
			    GUILayout.Label ("Screen distance", GUILayout.Width(100));

		    GUILayout.EndVertical();
		    GUILayout.Space(4);
		    GUILayout.BeginVertical();

			    GUILayout.Space(10);
                userIPD = GUILayout.HorizontalSlider(userIPD, 0, 100, GUILayout.Width(300));

			    GUILayout.Space(9);
			    virtualIPD = GUILayout.HorizontalSlider(virtualIPD, 0, 1000, GUILayout.Width(300));

			    GUILayout.Space(9);
			    hFOV = GUILayout.HorizontalSlider(hFOV, .1f, 179.9f, GUILayout.Width(300));

		    GUILayout.EndVertical();
		    GUILayout.BeginVertical();
			    GUILayout.BeginHorizontal();

				    if (GUILayout.Button("-", GUILayout.Width(20)))
					    userIPD -= .1f;

		            string userIPDString = StringCheck(userIPD.ToString());
				    fieldString = GUILayout.TextField(userIPDString, 5, GUILayout.Width(40));

                    if (fieldString != userIPDString)
				        userIPD = System.Convert.ToSingle(fieldString);

				    if (GUILayout.Button("+", GUILayout.Width(20)))
					    userIPD += .1f;

				    GUILayout.Label (" mm");

			    GUILayout.EndHorizontal();
			    GUILayout.BeginHorizontal();

				    if (GUILayout.Button("-", GUILayout.Width(20)))
					    virtualIPD -= 1f;

		            string virtualIPDString = StringCheck(virtualIPD.ToString());
				    fieldString = GUILayout.TextField(virtualIPDString, 5, GUILayout.Width(40));

                    if (fieldString != virtualIPDString)
				        virtualIPD = System.Convert.ToSingle(fieldString);

				    if (GUILayout.Button("+", GUILayout.Width(20)))
					    virtualIPD += 1f;

				    GUILayout.Label (" mm");

		            matchUserIPD = GUILayout.Toggle(matchUserIPD, " Match User IPD");

			    GUILayout.EndHorizontal();
			    GUILayout.BeginHorizontal();

				    if (GUILayout.Button("-", GUILayout.Width(20)))
					    hFOV -= .1f;

                    string hFOVString = StringCheck(hFOV.ToString());
				    fieldString = GUILayout.TextField(hFOVString, 5, GUILayout.Width(40));

                    if (fieldString != hFOVString)
				        hFOV = System.Convert.ToSingle(fieldString);

				    if (GUILayout.Button("+", GUILayout.Width(20)))
					    hFOV += .1f;

				    GUILayout.Label (" deg");

			    GUILayout.EndHorizontal();
			    GUILayout.BeginHorizontal();

			        GUILayout.Space(28);
				    GUILayout.TextField(vFOV.ToString(), 5, GUILayout.Width(40));

				    GUILayout.Label (" deg");

			    GUILayout.EndHorizontal();
			    GUILayout.BeginHorizontal();

			        GUILayout.Space(28);
				    GUILayout.TextField(screenDistance.ToString(), 5, GUILayout.Width(40));

				    GUILayout.Label (" mm");

			    GUILayout.EndHorizontal();
		    GUILayout.EndVertical();
        GUILayout.EndHorizontal();

        GUI.DragWindow(new Rect(0, 0, 640, 20)); //make GUI window draggable by top
    }

    string StringCheck(string str)
    {
        if (str.Length > 5)
            str = str.Substring(0, 5);

        return str;
    }
} 