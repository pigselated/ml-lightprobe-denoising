using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

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

public class TrainingDataGeneratorWindow : EditorWindow
{
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
    int scale = 2;
    int sampleCount = 128;
    bool useOIDN = false;
    bool useOptix = false;
    bool useGauss = false;
    bool useAtrous = false;
    bool isBaking = false;

    // Add menu item named "My Window" to the Window menu
    [MenuItem("Tools/Training Data Generator")]
    public static void ShowWindow()
    {
        //Show existing window instance. If one doesn't exist, make one.
        EditorWindow editorWindow = EditorWindow.GetWindow(typeof(TrainingDataGeneratorWindow));
        editorWindow.autoRepaintOnSceneChange = true;
        editorWindow.Show();
        editorWindow.title = "Training Data Generator";
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

        isBaking = false;
        RemoveLightmapperCallbacks();
    }

    void AddLightmapperCallbacks()
    {
        Lightmapping.started += Started;
        Lightmapping.completed += Completed;
    }

    void RemoveLightmapperCallbacks()
    {
        Lightmapping.started -= Started;
        Lightmapping.completed -= Completed;
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

            using (new EditorGUI.DisabledScope(useOptix))
            {
                useOIDN = EditorGUILayout.Toggle("Use OIDN", useOIDN);
            }

            using (new EditorGUI.DisabledScope(useOIDN))
            {
                useOptix = EditorGUILayout.Toggle("Use Optix", useOptix);
            }

            using (new EditorGUI.DisabledScope(useAtrous))
            {
                useGauss = EditorGUILayout.Toggle("Use GAUSSIAN", useGauss);
            }

            using (new EditorGUI.DisabledScope(useGauss))
            {
                useAtrous = EditorGUILayout.Toggle("Use A-TROUS", useAtrous);
            }
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

            path = string.Format("TrainingData_{0}_{1}_{2}", sampleCount, (useOIDN ? "OIDN" : (useOptix ? "OPTIX" : "NONE")), (useGauss ? "GAUSS" : (useAtrous ? "ATROUS" : "NONE")));
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
                LightmapEditorSettings.trainingDataDestination = path;
                LightmapEditorSettings.exportTrainingData = true;
                LightmapEditorSettings.directSampleCount = sampleCount;
                LightmapEditorSettings.indirectSampleCount = sampleCount;
                LightmapEditorSettings.environmentSampleCount = sampleCount;

                LightmapEditorSettings.filteringMode = LightmapEditorSettings.FilterMode.Advanced;
                if (useOIDN)
                {
                    LightmapEditorSettings.denoiserTypeDirect = LightmapEditorSettings.DenoiserType.OpenImage;
                    LightmapEditorSettings.denoiserTypeIndirect = LightmapEditorSettings.DenoiserType.OpenImage;
                    LightmapEditorSettings.denoiserTypeAO = LightmapEditorSettings.DenoiserType.OpenImage;
                }
                else if (useOptix)
                {
                    LightmapEditorSettings.denoiserTypeDirect = LightmapEditorSettings.DenoiserType.Optix;
                    LightmapEditorSettings.denoiserTypeIndirect = LightmapEditorSettings.DenoiserType.Optix;
                    LightmapEditorSettings.denoiserTypeAO = LightmapEditorSettings.DenoiserType.Optix;
                }
                else
                {
                    LightmapEditorSettings.denoiserTypeDirect = LightmapEditorSettings.DenoiserType.None;
                    LightmapEditorSettings.denoiserTypeIndirect = LightmapEditorSettings.DenoiserType.None;
                    LightmapEditorSettings.denoiserTypeAO = LightmapEditorSettings.DenoiserType.None;
                }

                if (useGauss)
                {
                    LightmapEditorSettings.filterTypeDirect = LightmapEditorSettings.FilterType.Gaussian;
                    LightmapEditorSettings.filterTypeIndirect = LightmapEditorSettings.FilterType.Gaussian;
                    LightmapEditorSettings.filterTypeAO = LightmapEditorSettings.FilterType.Gaussian;

                    LightmapEditorSettings.filteringGaussRadiusDirect = 1;
                    LightmapEditorSettings.filteringGaussRadiusIndirect = 5;
                    LightmapEditorSettings.filteringGaussRadiusAO = 2;
                }
                else if (useAtrous)
                {
                    LightmapEditorSettings.filterTypeDirect = LightmapEditorSettings.FilterType.ATrous;
                    LightmapEditorSettings.filterTypeIndirect = LightmapEditorSettings.FilterType.ATrous;
                    LightmapEditorSettings.filterTypeAO = LightmapEditorSettings.FilterType.ATrous;

                    LightmapEditorSettings.filteringAtrousPositionSigmaDirect = 0.5F;
                    LightmapEditorSettings.filteringAtrousPositionSigmaIndirect = 2.0F;
                    LightmapEditorSettings.filteringAtrousPositionSigmaAO = 1.0F;
                }
                else
                {
                    LightmapEditorSettings.filterTypeDirect = LightmapEditorSettings.FilterType.None;
                    LightmapEditorSettings.filterTypeIndirect = LightmapEditorSettings.FilterType.None;
                    LightmapEditorSettings.filterTypeAO = LightmapEditorSettings.FilterType.None;
                }

                Debug.ClearDeveloperConsole();
                Lightmapping.BakeAsync();
            }
        }
    }
}

