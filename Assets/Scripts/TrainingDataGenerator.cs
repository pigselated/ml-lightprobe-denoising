using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System.IO;

[ExecuteInEditMode]
public class Bake : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

[System.Serializable]
public class LightProbeTrainingData
{
    public string description;
    public int probeCount;
    public int sampleCount;

    // Notation:
    // http://www.ppsloan.org/publications/StupidSH36.pdf
    // http://graphics.stanford.edu/papers/envmap/envmap.pdf
    //
    //                       [L00:  DC]
    //            [L1-1:  y] [L10:   z] [L11:   x]
    // [L2-2: xy] [L2-1: yz] [L20:  zz] [L21:  xz]  [L22:  xx - yy]
    //
    // 9 coefficients for R, G and B ordered:
    // {  L00, L1-1,  L10,  L11, L2-2, L2-1,  L20,  L21,  L22,  // red   channel
    //    L00, L1-1,  L10,  L11, L2-2, L2-1,  L20,  L21,  L22,  // blue  channel
    //    L00, L1-1,  L10,  L11, L2-2, L2-1,  L20,  L21,  L22 } // green channel
    public SphericalHarmonicsL2[] coefficients;
    public Vector3[] positions;
}

public class TrainingDataGeneratorWindow : EditorWindow
{
    int ToNearest(int x)
    {
        return (int)Mathf.Pow(2.0f, Mathf.Round(Mathf.Log(x) / Mathf.Log(2.0f)));
    }

    void TakeHiResShot(string filename, Camera camera, int width, int height)
    {
        RenderTexture rt = new RenderTexture(width, height, 24);
        camera.targetTexture = rt;
        TextureFormat tFormat = TextureFormat.RGB24;

        Texture2D screenShot = new Texture2D(width, height, tFormat, false);
        camera.Render();
        RenderTexture.active = rt;
        screenShot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        camera.targetTexture = null;
        RenderTexture.active = null;
        byte[] bytes = screenShot.EncodeToPNG();

        System.IO.File.WriteAllBytes(filename, bytes);
        Debug.Log(string.Format("Took screenshot to: {0}", filename));
    }

    string path = "";

    int resWidth = 1920;
    int resHeight = 1080;
    int scale = 4;
    int sampleCount = 8;
    bool isBaking = false;

    [MenuItem("Tools/Training Data Generator")]
    public static void ShowWindow()
    {
        //Show existing window instance. If one doesn't exist, make one.
        EditorWindow editorWindow = EditorWindow.GetWindow(typeof(TrainingDataGeneratorWindow));
        editorWindow.autoRepaintOnSceneChange = true;
        editorWindow.Show();
        editorWindow.titleContent.text = "Training Data Generator";
    }
    void Started()
    {
        Lightmapping.Clear();
    }

    void Completed()
    {
        // Take screenshots
        Camera[] cameras = Camera.allCameras;
        foreach (Camera camera in cameras)
        {
            if (camera.name.ToLower().Contains("screenshot"))
            {
                string strPath = "";
                strPath = string.Format("{0}/screen_{1}.png", path, camera.name);
                TakeHiResShot(strPath, camera, resWidth * scale, resHeight * scale);
            }
        }

        // Export lightprobe data
        LightProbeTrainingData data = new LightProbeTrainingData();
        data.description = "The layout is as follows, 9 red coefficients followed by green and blue coefficients. Coefficient 0 is the DC band. This is followed by band 1 and band 2.";
        data.probeCount = LightmapSettings.lightProbes.count;
        data.sampleCount = sampleCount;
        data.coefficients = LightmapSettings.lightProbes.bakedProbes;
        data.positions = LightmapSettings.lightProbes.positions;
        string dataPath = string.Format("{0}/LPTrainingData_{1}.json", path, sampleCount);
        if (System.IO.Directory.Exists(dataPath))
        {
            System.IO.Directory.Delete(dataPath, true);
        }
        File.WriteAllText(dataPath, JsonUtility.ToJson(data));

        isBaking = false;
        RemoveLightmapperCallbacks();
    }

    void AddLightmapperCallbacks()
    {
        Lightmapping.bakeStarted += Started;
        Lightmapping.bakeCompleted += Completed;
    }

    void RemoveLightmapperCallbacks()
    {
        Lightmapping.bakeStarted -= Started;
        Lightmapping.bakeCompleted -= Completed;
    }
    void OnInspectorUpdate()
    {
        Repaint();
    }

    void OnEnable()
    {
        Lightmapping.giWorkflowMode = Lightmapping.GIWorkflowMode.OnDemand;
    }

    void OnGUI()
    {
        using (new EditorGUI.DisabledScope(isBaking))
        {
            sampleCount = EditorGUILayout.IntField("Sample count", sampleCount);
            sampleCount = Mathf.Max(ToNearest(sampleCount), 8);
        }

        EditorGUILayout.Space();

        bool bake = false;
        using (new EditorGUI.DisabledScope(isBaking))
        {
            float progress = Lightmapping.buildProgress;
            bake = GUILayout.Button((isBaking ? ("Baking... " + ((int)(100 * progress)).ToString() + "%") : "Bake"), GUILayout.MinHeight(60));
        }

        if (bake && !isBaking)
        {
            isBaking = true;
            AddLightmapperCallbacks();

            path = string.Format("TrainingData_{0}", sampleCount);
            if (System.IO.Directory.Exists(path))
            {
                System.IO.Directory.Delete(path, true);
            }
            System.IO.Directory.CreateDirectory(path);

            if (!System.IO.Directory.Exists(path))
            {
                Debug.LogError("Missing path: " + path);
            }
            else
            {
                EditorSettings.useLegacyProbeSampleCount = false;


                LightmapEditorSettings.trainingDataDestination = path;
                LightmapEditorSettings.exportTrainingData = true;
                LightmapEditorSettings.directSampleCount = sampleCount;
                LightmapEditorSettings.indirectSampleCount = sampleCount;
                LightmapEditorSettings.environmentSampleCount = sampleCount;
                LightmapEditorSettings.lightProbeSampleCountMultiplier = 1;

                LightmapEditorSettings.filteringMode = LightmapEditorSettings.FilterMode.Advanced;

                LightmapEditorSettings.denoiserTypeDirect = LightmapEditorSettings.DenoiserType.None;
                LightmapEditorSettings.denoiserTypeIndirect = LightmapEditorSettings.DenoiserType.None;
                LightmapEditorSettings.denoiserTypeAO = LightmapEditorSettings.DenoiserType.None;

                LightmapEditorSettings.filterTypeDirect = LightmapEditorSettings.FilterType.None;
                LightmapEditorSettings.filterTypeIndirect = LightmapEditorSettings.FilterType.None;
                LightmapEditorSettings.filterTypeAO = LightmapEditorSettings.FilterType.None;
        
                Debug.ClearDeveloperConsole();
                Lightmapping.BakeAsync();
            }
        }
    }
}

