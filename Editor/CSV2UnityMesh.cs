using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Formats.Fbx.Exporter;
using System.IO;
using System.Linq;
using System;

namespace UnityEditor.CSV2UnityMesh
{
    public class CSV2UnityMesh : EditorWindow
    {
        private SerializedObject serializedObject;
        private SerializedProperty csvAssetProp;
        private SerializedProperty materialDebugModeProp;

        public static int positionColumnID = 3;
        public static int normalColumnID = 6;
        public static int tangentColumnID = 9;
        public static int colorColumnID = 13;
        public static int[] texcoordColumnID = new int[] {17 , 19, 21, 23, 25};

        public static float modelScale = 1.0f;
        public static bool flipNormals = false;
        public static bool flipUV = false;
        public static bool[] enableTexcoord = new bool[] {true, false, false, false, false};

        private string[] m_columnHeadsArray = null;

        public TextAsset m_csvAsset = null;

        

        private string m_outFilePath = "Assets/CSV2UnityMesh/";
        private string m_outFileName = "outfile.fbx";

        private static readonly GUID debugShaderGUID = new GUID("86e38963fcf6c9d47952280214f1d1c1");
        private static readonly GUID debugMaterialGUID = new GUID("a97c6f0fc94b8c14e979667a1dcc2dda");
        private static Material debugMaterial;

        // Preview
        private PreviewRenderUtility previewRenderUtility;
        private Mesh targetMesh;
        private Vector2 previewDir = new Vector2(120, -20);
        private float zoom = 5f;
        private Vector3 objectPosition = Vector3.zero;
        private Vector2 dragStartPos;
        public enum MaterialDebugMode
        {
            BasicLighting,
            Normal,
            Tangent,
            VertexColor,
            UV
        }
        public MaterialDebugMode m_materialDebugMode;
        public enum MaterialOutputChannel
        {
            None = 0,
            Red = 1 << 0,    // 1
            Green = 1 << 1,  // 2
            Blue = 1 << 2,   // 4
            Alpha = 1 << 3   // 8
        }
        public MaterialOutputChannel m_materialOutputChannel = (MaterialOutputChannel)7;

        [MenuItem("Tools/CSV2UnityMesh")]
        public static void ShowWindow()
        {
            var window = EditorWindow.GetWindow(typeof(CSV2UnityMesh));
            window.position = new Rect(800, 300, 500, 809);
        }

        private void OnEnable()
        {
            ReadCSVAssetToHeadsArray(m_csvAsset, out m_columnHeadsArray, out m_outFileName);

            serializedObject = new SerializedObject(this);
            csvAssetProp = serializedObject.FindProperty("m_csvAsset");
            materialDebugModeProp = serializedObject.FindProperty("m_materialDebugMode");

            debugMaterial = (Material)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(debugMaterialGUID), typeof(Material));

            previewRenderUtility = new PreviewRenderUtility();
            previewRenderUtility.cameraFieldOfView = 30f;
            previewRenderUtility.camera.transform.position = new Vector3(0, 0, -zoom);
            previewRenderUtility.camera.transform.LookAt(Vector3.zero);
            previewRenderUtility.camera.farClipPlane = 1000.0f;
            previewRenderUtility.camera.nearClipPlane = 0.03f;
        }

        private void OnGUI()
        {
            serializedObject.Update();
            GUILayout.Space(10);

            float tempLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 100;

            // csvAsset
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField(Styles.csvAsset, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(csvAssetProp);
            GUILayout.Space(10);
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                ReadCSVAssetToHeadsArray(m_csvAsset, out m_columnHeadsArray, out m_outFileName);
                targetMesh = null;
            }

            GUI.enabled = m_csvAsset != null;


            // Mesh properties
            EditorGUILayout.LabelField(Styles.meshProperties, EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical();

            EditorGUILayout.BeginHorizontal();
            positionColumnID    = EditorGUILayout.Popup(Styles.positionStr, positionColumnID , m_columnHeadsArray, EditorStyles.popup, GUILayout.Width(250));
            GUILayout.FlexibleSpace(); modelScale = EditorGUILayout.FloatField(Styles.scale, modelScale, GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            normalColumnID      = EditorGUILayout.Popup(Styles.normalStr, normalColumnID    , m_columnHeadsArray, EditorStyles.popup, GUILayout.Width(250));
            GUILayout.FlexibleSpace(); flipNormals = EditorGUILayout.Toggle(Styles.flipNormal, flipNormals, GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();

            tangentColumnID     = EditorGUILayout.Popup(Styles.tangentStr, tangentColumnID  , m_columnHeadsArray, EditorStyles.popup, GUILayout.Width(250));
            colorColumnID       = EditorGUILayout.Popup(Styles.colorStr, colorColumnID      , m_columnHeadsArray, EditorStyles.popup, GUILayout.Width(250));

            
            for (int ti = 0; ti < texcoordColumnID.Length; ti++)
            {
                EditorGUILayout.BeginHorizontal();
                if (enableTexcoord[ti])
                {
                    texcoordColumnID[ti] = EditorGUILayout.Popup(Styles.texcoordStr + ti + ":", texcoordColumnID[ti], m_columnHeadsArray, EditorStyles.popup, GUILayout.Width(250));
                }
                else
                {
                    var tempEnable = GUI.enabled;
                    GUI.enabled = false;
                    texcoordColumnID[ti] = EditorGUILayout.Popup(Styles.texcoordStr + ti + ":", texcoordColumnID[ti], m_columnHeadsArray, EditorStyles.popup, GUILayout.Width(250));
                    GUI.enabled = tempEnable;
                }
                    
                GUILayout.FlexibleSpace(); enableTexcoord[ti] = EditorGUILayout.Toggle(Styles.enableTexcoord, enableTexcoord[ti], GUILayout.Width(150));
                EditorGUILayout.EndHorizontal();
            }


            EditorGUIUtility.labelWidth = tempLabelWidth;
            EditorGUILayout.EndVertical();


            #region Preview Mesh

            if (targetMesh != null)
            {
                GUILayout.Space(20);

                Rect previewRect = GUILayoutUtility.GetRect(200, 200, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

                // Handle mouse events for rotation
                HandleMouseEvents(previewRect);

                previewRenderUtility.BeginPreview(previewRect, GUIStyle.none);

                // Configure the camera and render the mesh
                Matrix4x4 trs = Matrix4x4.TRS(objectPosition, Quaternion.identity, Vector3.one * 5);
                previewRenderUtility.camera.transform.position = Quaternion.Euler(previewDir.y, previewDir.x, 0) * new Vector3(0, 0, -zoom);
                previewRenderUtility.camera.transform.LookAt(Vector3.zero);
                previewRenderUtility.DrawMesh(targetMesh, trs, debugMaterial, 0);
                previewRenderUtility.camera.Render();

                Texture resultRender = previewRenderUtility.EndPreview();
                GUI.DrawTexture(previewRect, resultRender, ScaleMode.StretchToFill, false);
            }
            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(materialDebugModeProp);
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();

                SetMaterialDebugMode(debugMaterial, m_materialDebugMode);
            }

            GUILayout.Space(50);

            uint channelMask = (uint)m_materialOutputChannel;
            if (GUILayout.Button("R", GetButtonStyle((channelMask & (1 << 0)) != 0), GUILayout.Width(30)))
            {
                m_materialOutputChannel = (MaterialOutputChannel)(channelMask ^ (1 << 0));
                SetMaterialOutPutChannel(debugMaterial, m_materialOutputChannel);
            }

            if (GUILayout.Button("G", GetButtonStyle((channelMask & (1 << 1)) != 0), GUILayout.Width(30)))
            {
                m_materialOutputChannel = (MaterialOutputChannel)(channelMask ^ (1 << 1));
                SetMaterialOutPutChannel(debugMaterial, m_materialOutputChannel);
            }

            if (GUILayout.Button("B", GetButtonStyle((channelMask & (1 << 2)) != 0), GUILayout.Width(30)))
            {
                m_materialOutputChannel = (MaterialOutputChannel)(channelMask ^ (1 << 2));
                SetMaterialOutPutChannel(debugMaterial, m_materialOutputChannel);
            }

            if (GUILayout.Button("A", GetButtonStyle((channelMask & (1 << 3)) != 0), GUILayout.Width(30)))
            {
                m_materialOutputChannel = (MaterialOutputChannel)(channelMask ^ (1 << 3));
                SetMaterialOutPutChannel(debugMaterial, m_materialOutputChannel);
            }

            GUILayout.EndHorizontal();

            #endregion Preview Mesh


            #region SaveFileGUI
            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label("SavePath: ", EditorStyles.boldLabel, GUILayout.Width(80));
            EditorGUILayout.TextArea(m_outFilePath, GUILayout.Width(200));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("OutName: ", EditorStyles.boldLabel, GUILayout.Width(80));
            EditorGUILayout.TextArea(m_outFileName, GUILayout.Width(200));
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();


            if (GUILayout.Button("Select Path", GUILayout.Height(40)))
            {
                string selectedPath = EditorUtility.SaveFolderPanel("Select an output path", "Assets", "defaultFold");
                if (!String.IsNullOrEmpty(selectedPath))
                {
                    selectedPath = ConvertToRelativePath(selectedPath);
                    m_outFilePath = selectedPath + "/";
                }
            }

            GUILayout.EndHorizontal();
            #endregion


            GUILayout.Space(10);
            if (GUILayout.Button("Generate Mesh", GUILayout.Height(30)))
            {
                Mesh genMesh = CreateMeshFromCSVAsset(m_columnHeadsArray, m_csvAsset);
                if (genMesh != null)
                {
                    CreateMeshAssetAndShow(genMesh, m_outFilePath, m_outFileName);
                    //GameObject.Destroy(genMesh);

                    string exportPath = Path.Combine(m_outFilePath, m_outFileName);
                    targetMesh = AssetDatabase.LoadAssetAtPath<Mesh>(exportPath);
                    SetMaterialDebugMode(debugMaterial, m_materialDebugMode);
                }
            }

            GUI.enabled = true;

            GUILayout.Space(30);
            serializedObject.ApplyModifiedProperties();
        }

        private GUIStyle GetButtonStyle(bool active)
        {
            GUIStyle style = new GUIStyle(GUI.skin.box);
            if (active)
            {
                style.normal.background = Texture2D.whiteTexture;
            }
            else
            {
                style.normal.textColor = Color.black;
                style.active.textColor = Color.black;
                style.focused.textColor = Color.black;
                style.hover.textColor = Color.black;
            }
            return style;
        }
        private static void ReadCSVAssetToHeadsArray(TextAsset csvAsset, out string[] columnHeadsArray, out string fileName)
        {
            if (csvAsset != null)
            {
                // Read asset to columnHeadsArray
                var dataHeads = ReadCSVColumnHeads(csvAsset);
                columnHeadsArray = dataHeads.ToArray();
                fileName = csvAsset.name + ".fbx";
            }
            else
            {
                columnHeadsArray = new string[]
                {
                    Styles.VTXStr, Styles.IDXStr,
                    Styles.positionStr, Styles.positionStr, Styles.positionStr,
                    Styles.normalStr, Styles.normalStr, Styles.normalStr,
                    Styles.tangentStr, Styles.tangentStr, Styles.tangentStr, Styles.tangentStr,
                    Styles.colorStr, Styles.colorStr, Styles.colorStr, Styles.colorStr,
                    Styles.texcoordStr + "0", Styles.texcoordStr + "0",
                    Styles.texcoordStr + "1", Styles.texcoordStr + "1",
                    Styles.texcoordStr + "2", Styles.texcoordStr + "2",
                    Styles.texcoordStr + "3", Styles.texcoordStr + "3",
                    Styles.texcoordStr + "4", Styles.texcoordStr + "4",
                };
                fileName = "noneFileName.fbx";
            }
        }

        public static List<string> ReadCSVColumnHeads(TextAsset csvAsset)
        {
            string assetPath = AssetDatabase.GetAssetPath(csvAsset);
            if (!System.IO.File.Exists(assetPath))
                return null;

            string clipboard = System.IO.File.ReadAllText(assetPath);
            var allTexts = clipboard.Split('\n');

            var heads = allTexts[0].Trim().Replace(" ", "").Split(',');

            return heads.Select(key => key.Contains(".") ? key.Split('.')[0] : key).ToList();
        }

        public static Mesh CreateMeshFromCSVAsset(string[] csvColumnHeads, TextAsset csvAsset, bool rotation90 = false, bool flipUV = false, bool flipNormals = false)
        {
            var assetPath = AssetDatabase.GetAssetPath(csvAsset);
            if (!System.IO.File.Exists(assetPath))
            {
                Debug.LogError("No csv files at path: " + assetPath);
                return null;
            }

            string clipboard = System.IO.File.ReadAllText(assetPath);
            var allTexts = clipboard.Split('\n');

            if (allTexts.Length <= 1)
            {
                Debug.LogError("Csv files length flase: " + allTexts.Length);
                return null;
            }
            var heads = allTexts[0].Trim().Replace(" ", "").Split(',');
            List<float[]> allRows = new List<float[]>();
            ReadAllRows(allTexts, heads.Length, ref allRows);

            var IDX = GetColumnIndex(heads, "IDX");
            var positionColumnX = GetColumnIndex(heads, csvColumnHeads[positionColumnID] + ".x");
            var positionColumnY = GetColumnIndex(heads, csvColumnHeads[positionColumnID] + ".y");
            var positionColumnZ = GetColumnIndex(heads, csvColumnHeads[positionColumnID] + ".z");

            var normalColumnX = GetColumnIndex(heads, csvColumnHeads[normalColumnID] + ".x");
            var normalColumnY = GetColumnIndex(heads, csvColumnHeads[normalColumnID] + ".y");
            var normalColumnZ = GetColumnIndex(heads, csvColumnHeads[normalColumnID] + ".z");

            var tangentColumnX = GetColumnIndex(heads, csvColumnHeads[tangentColumnID] + ".x");
            var tangentColumnY = GetColumnIndex(heads, csvColumnHeads[tangentColumnID] + ".y");
            var tangentColumnZ = GetColumnIndex(heads, csvColumnHeads[tangentColumnID] + ".z");
            var tangentColumnW = GetColumnIndex(heads, csvColumnHeads[tangentColumnID] + ".w");

            var colorColumnX = GetColumnIndex(heads, csvColumnHeads[colorColumnID] + ".x");
            var colorColumnY = GetColumnIndex(heads, csvColumnHeads[colorColumnID] + ".y");
            var colorColumnZ = GetColumnIndex(heads, csvColumnHeads[colorColumnID] + ".z");
            var colorColumnW = GetColumnIndex(heads, csvColumnHeads[colorColumnID] + ".w");

            int[] texcoordColumnX = new int[] { -1, -1, -1, -1, -1 };
            int[] texcoordColumnY = new int[] { -1, -1, -1, -1, -1 };
            int[] texcoordColumnZ = new int[] { -1, -1, -1, -1, -1 };
            int[] texcoordColumnW = new int[] { -1, -1, -1, -1, -1 };

            for ( int ti = 0; ti < texcoordColumnID.Length; ti++ )
            {
                if (!enableTexcoord[ti])
                    continue;

                texcoordColumnX[ti] = GetColumnIndex(heads, csvColumnHeads[texcoordColumnID[ti]] + ".x");
                texcoordColumnY[ti] = GetColumnIndex(heads, csvColumnHeads[texcoordColumnID[ti]] + ".y");
                texcoordColumnZ[ti] = GetColumnIndex(heads, csvColumnHeads[texcoordColumnID[ti]] + ".z");
                texcoordColumnW[ti] = GetColumnIndex(heads, csvColumnHeads[texcoordColumnID[ti]] + ".w");
            }


            if (IDX < 0 || positionColumnX < 0 || positionColumnY < 0 || positionColumnZ < 0)
            {
                Debug.Log("Position data error.");
                return null;
            }
            bool hasNormalProp = (normalColumnX >= 0 && normalColumnY >= 0 && normalColumnZ >= 0);
            bool hasTangentProp = (tangentColumnX >= 0 && tangentColumnY >= 0 && tangentColumnZ >= 0 && tangentColumnW >= 0);
            bool hasColorProp = (colorColumnX >= 0 && colorColumnY >= 0 && colorColumnZ >= 0 && colorColumnW >= 0);

            int minIndex = int.MaxValue;
            int maxIndex = -1;
            for (int i = 0; i < allRows.Count; ++i)
            {
                int currIndex = (int)allRows[i][IDX];
                if (currIndex < minIndex)
                {
                    minIndex = currIndex;
                }
                else if (currIndex > maxIndex)
                {
                    maxIndex = currIndex;
                }
            }

            int vertexLength = maxIndex - minIndex + 1; // Container Self Index.
            int indexLen = allRows.Count;
            if (indexLen % 3 != 0)
            {
                Debug.Log("vertex Length is zero.");
                return null;
            }

            Vector3[] vertices     = new Vector3[vertexLength];
            Vector3[] normals       = new Vector3[vertexLength];
            Vector4[] tangents      = new Vector4[vertexLength];
            Color[]   vertexColors  = new Color[vertexLength];
            List<Vector4[]> vertexTexcoords = new List<Vector4[]>();
            for (int ti = 0; ti < texcoordColumnID.Length; ti++)
            {
                if (!enableTexcoord[ti])
                    continue;
                vertexTexcoords.Add(new Vector4[vertexLength]);
            }

            int[] outputIndexBuff = new int[indexLen];
            var rotationN90 = rotation90 ? Quaternion.Euler(-90, 0, 0) : Quaternion.identity;
            for (int i = 0; i < allRows.Count; ++i)
            {
                var currLine = allRows[i];
                var realIndex = (int)currLine[IDX] - minIndex;
                outputIndexBuff[i] = realIndex;
                if (realIndex < vertices.Length && realIndex >= 0)
                {
                    var p = new Vector3(currLine[positionColumnX], currLine[positionColumnY], currLine[positionColumnZ]);
                    vertices[realIndex] = rotationN90 * p;

                    vertices[realIndex].x *= modelScale;
                    vertices[realIndex].y *= modelScale;
                    vertices[realIndex].z *= modelScale;

                    if (hasNormalProp)
                    {
                        var nor = new Vector3(currLine[normalColumnX], currLine[normalColumnY], currLine[normalColumnZ]);
                        normals[realIndex] = rotationN90 * nor;
                    }

                    if (hasTangentProp)
                    {
                        tangents[realIndex] = new Vector4(currLine[tangentColumnX], currLine[tangentColumnY], currLine[tangentColumnZ], currLine[tangentColumnW]);
                    }

                    if (hasColorProp)
                    {
                        vertexColors[realIndex] = new Color(currLine[colorColumnX], currLine[colorColumnY], currLine[colorColumnZ], currLine[colorColumnW]);
                    }

                    for (int ti = 0; ti < texcoordColumnID.Length; ti++)
                    {
                        if (!enableTexcoord[ti])
                            continue;
                        vertexTexcoords[ti][realIndex] = new Vector4(
                            texcoordColumnX[ti] < 0 ? float.MinValue : currLine[texcoordColumnX[ti]],
                            texcoordColumnY[ti] < 0 ? float.MinValue : currLine[texcoordColumnY[ti]],
                            texcoordColumnZ[ti] < 0 ? float.MinValue : currLine[texcoordColumnZ[ti]],
                            texcoordColumnW[ti] < 0 ? float.MinValue : currLine[texcoordColumnW[ti]]);
                    }

                }
                else
                {
                    return null;
                }
            }

            Mesh mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.SetTriangles(outputIndexBuff, 0);

            for (int ti = 0; ti < texcoordColumnID.Length; ti++)
            {
                if (!enableTexcoord[ti])
                    continue;
                mesh.SetUVs(ti, vertexTexcoords[ti]);
            }


            if (hasNormalProp)
            {
                mesh.normals = normals;
            }
            else
            {
                mesh.RecalculateNormals();
            }

            if (hasNormalProp)
            {
                mesh.tangents = tangents;
            }
            else
            {
                mesh.RecalculateTangents();
            }

            if (hasColorProp)
            {
                mesh.colors = vertexColors;
            }

            if (flipNormals)
            {
                mesh.triangles = mesh.triangles.Reverse().ToArray();
            }

            return mesh;
        }

        private static void CreateMeshAssetAndShow(Mesh mesh, string filePath, string fileName)
        {
            string exportPath = Path.Combine(filePath, fileName);

            //var shader = (Shader)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(debugShaderGUID), typeof(Shader));
            //Material material = new Material(shader);
            //Material material = (Material)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(debugMaterialGUID), typeof(Material));

            GameObject obj = new GameObject();
            var meshFilter = obj.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var meshRenderer = obj.AddComponent<MeshRenderer>();
            //meshRenderer.sharedMaterial = material; // Use default Lit

            obj.name = fileName.Split('.')[0] + "Mesh";
            ModelExporter.ExportObject(exportPath, obj);
            SaveMeshToAsset(mesh, filePath + fileName + "_fullData.mesh");

            // Clean
            GameObject.DestroyImmediate(obj);

            // Ping Object
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath(exportPath, typeof(UnityEngine.Object)));
        }

        private static void ReadAllRows(string[] allTexts, int headsLength, ref List<float[]> allRows)
        {
            foreach (var lineText in allTexts.Skip(1))
            {
                if (lineText.Length <= 10)
                {
                    continue;
                }

                var cells = lineText.Trim().Replace(" ", "").Split(',');
                if (cells.Length != headsLength)
                {
                    continue;
                }

                float[] cellData = new float[cells.Length];
                for (int i = 0; i < cells.Length; ++i)
                {
                    if (!float.TryParse(cells[i], out cellData[i]))
                    {
                        Debug.Log("Don't have csv data.");
                    }
                }

                allRows.Add(cellData);
            }
        }

        public static int GetColumnIndex(string[] input, string key)
        {
            for (int i = 0; i < input.Length; ++i)
            {
                if (input[i] == key)
                {
                    return i;
                }
            }

            return -1;
        }

        private string ConvertToRelativePath(string absolutePath)
        {
            string relativePath = absolutePath;

            if (absolutePath.StartsWith(Application.dataPath))
            {
                relativePath = "Assets" + absolutePath.Substring(Application.dataPath.Length);
            }

            return relativePath;
        }

        private static class Styles
        {
            public static readonly GUIContent csvAsset = EditorGUIUtility.TrTextContent("csv Asset");
            public static readonly GUIContent scale = EditorGUIUtility.TrTextContent("scale");
            public static readonly GUIContent flipNormal = EditorGUIUtility.TrTextContent("flipNormal");
            public static readonly GUIContent flipUV = EditorGUIUtility.TrTextContent("flipUV");
            public static readonly GUIContent enableTexcoord = EditorGUIUtility.TrTextContent("Enable");


            public static readonly GUIContent meshProperties = EditorGUIUtility.TrTextContent("Mesh Properties");

            public static string VTXStr = "VTX";
            public static string IDXStr = "IDX";
            public static string positionStr = "Position:";
            public static string normalStr = "Normal:";
            public static string tangentStr = "Tangent:";
            public static string colorStr = "Color:";
            public static string texcoordStr = "Texcoord";


            public static string materialDebugMode = "MaterialDebugMode:";
        }


        private void HandleMouseEvents(Rect previewRect)
        {
            Event e = Event.current;
            if ((e.isMouse || e.isScrollWheel) && previewRect.Contains(e.mousePosition))
            {
                switch (e.type)
                {
                    case EventType.MouseDown:
                        if (e.button == 0) // 左键拖动旋转
                        {
                            dragStartPos = e.mousePosition;
                            e.Use();
                        }
                        else if (e.button == 2) // 中键拖动物体位置
                        {
                            dragStartPos = e.mousePosition;
                            e.Use();
                        }
                        break;

                    case EventType.MouseDrag:
                        if (e.button == 0) // 左键拖动旋转
                        {
                            Vector2 delta = e.mousePosition - dragStartPos;
                            previewDir += delta * 0.5f; // 调整旋转速度
                            dragStartPos = e.mousePosition;
                            e.Use();
                        }
                        else if (e.button == 2) // 中键拖动物体位置
                        {
                            Vector2 delta = e.mousePosition - dragStartPos;
                            objectPosition += new Vector3(delta.x * 0.01f, -delta.y * 0.01f, 0);
                            dragStartPos = e.mousePosition;
                            e.Use();
                        }
                        break;

                    case EventType.ScrollWheel: // 滚轮缩放
                        zoom += e.delta.y * 0.1f;
                        zoom = Mathf.Clamp(zoom, 1f, 10f);
                        e.Use();
                        break;
                }
            }
        }

        private void Update()
        {
            Repaint();
        }

        private void OnDisable()
        {
            if (previewRenderUtility != null)
            {
                previewRenderUtility.Cleanup();
                previewRenderUtility = null;
            }
        }

        static void SetMaterialDebugMode(Material material, MaterialDebugMode debugMode)
        {
            if (material != null)
            {
                material.DisableKeyword("_BASIC_LIGHTING");
                material.DisableKeyword("_NORMAL_DEBUG");
                material.DisableKeyword("_TANGENT_DEBUG");
                material.DisableKeyword("_VERTEX_COLOR_DEBUG");
                material.DisableKeyword("_UV_DEBUG");

                switch (debugMode)
                {
                    case MaterialDebugMode.BasicLighting:
                        material.EnableKeyword("_BASIC_LIGHTING");
                        break;
                    case MaterialDebugMode.Normal:
                        material.EnableKeyword("_NORMAL_DEBUG");
                        break;
                    case MaterialDebugMode.Tangent:
                        material.EnableKeyword("_TANGENT_DEBUG");
                        break;
                    case MaterialDebugMode.VertexColor:
                        material.EnableKeyword("_VERTEX_COLOR_DEBUG");
                        break;
                    case MaterialDebugMode.UV:
                        material.EnableKeyword("_UV_DEBUG");
                        break;
                    default:
                        break;
                }

                EditorUtility.SetDirty(material);
            }
        }

        static void SetMaterialOutPutChannel(Material material, MaterialOutputChannel outputChannel)
        {
            // Disable all output channels keywords
            material.DisableKeyword("_OUTPUT_RED");
            material.DisableKeyword("_OUTPUT_GREEN");
            material.DisableKeyword("_OUTPUT_BLUE");
            material.DisableKeyword("_OUTPUT_ALPHA");

            // Enable the specific output channels based on the mask
            if ((outputChannel & MaterialOutputChannel.Red) != 0)
                material.EnableKeyword("_OUTPUT_RED");

            if ((outputChannel & MaterialOutputChannel.Green) != 0)
                material.EnableKeyword("_OUTPUT_GREEN");

            if ((outputChannel & MaterialOutputChannel.Blue) != 0)
                material.EnableKeyword("_OUTPUT_BLUE");

            if ((outputChannel & MaterialOutputChannel.Alpha) != 0)
                material.EnableKeyword("_OUTPUT_ALPHA");

            EditorUtility.SetDirty(material);
        }

        private static void SaveMeshToAsset(Mesh mesh, string filePath)
        {
            AssetDatabase.CreateAsset(mesh, filePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
