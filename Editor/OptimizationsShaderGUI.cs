using UnityEngine;
using UnityEditor;

public class OptimizationsShaderGUI : ShaderGUI
{
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        // Draw the default inspector
        base.OnGUI(materialEditor, properties);

        // Add a toggle for ZClip
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Optimization Settings", EditorStyles.boldLabel);
        
        Material targetMat = materialEditor.target as Material;
        bool zClipOff = targetMat.IsKeywordEnabled("_ZCLIP_OFF");
        
        EditorGUI.BeginChangeCheck();
        zClipOff = EditorGUILayout.Toggle("Disable Z Clipping", zClipOff);
        if (EditorGUI.EndChangeCheck())
        {
            // Enable or disable the keyword based on the toggle
            if (zClipOff)
                targetMat.EnableKeyword("_ZCLIP_OFF");
            else
                targetMat.DisableKeyword("_ZCLIP_OFF");
            
            EditorUtility.SetDirty(targetMat);
        }

        // Add some help text
        EditorGUILayout.HelpBox(
            "Culling: Back (Standard)\n" +
            "ZWrite: On\n" +
            "ZTest: LEqual\n" +
            "Disable Z Clipping: " + (zClipOff ? "Yes" : "No") + "\n\n" +
            "Note: Disabling Z Clipping can help with rendering far objects, but may cause visual artifacts.", 
            MessageType.Info);
    }
}
