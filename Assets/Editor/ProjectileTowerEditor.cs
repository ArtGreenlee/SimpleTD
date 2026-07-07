using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ProjectileTower))]
public class ProjectileTowerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (iterator.name == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(iterator, true);
                }
                continue;
            }

            if (iterator.name == "projectileSpeedMode")
            {
                EditorGUILayout.PropertyField(iterator, true);

                SerializedProperty mode = iterator;
                SerializedProperty bulletSpeed = serializedObject.FindProperty("bulletSpeed");
                SerializedProperty minSpeed = serializedObject.FindProperty("projectileMinSpeed");
                SerializedProperty maxSpeed = serializedObject.FindProperty("projectileMaxSpeed");

                var selectedMode = (ProjectileTower.ProjectileSpeedMode)mode.enumValueIndex;
                if (selectedMode == ProjectileTower.ProjectileSpeedMode.Constant)
                {
                    EditorGUILayout.PropertyField(bulletSpeed, true);
                }
                else
                {
                    EditorGUILayout.PropertyField(minSpeed, true);
                    EditorGUILayout.PropertyField(maxSpeed, true);
                }
                continue;
            }

            if (iterator.name == "projectileAccelerationMode")
            {
                EditorGUILayout.PropertyField(iterator, true);

                SerializedProperty mode = iterator;
                SerializedProperty constant = serializedObject.FindProperty("projectileAcceleration");
                SerializedProperty min = serializedObject.FindProperty("projectileAccelerationMin");
                SerializedProperty max = serializedObject.FindProperty("projectileAccelerationMax");

                var selectedMode = (ProjectileTower.ValueMode)mode.enumValueIndex;
                if (selectedMode == ProjectileTower.ValueMode.Constant)
                {
                    EditorGUILayout.PropertyField(constant, true);
                }
                else
                {
                    EditorGUILayout.PropertyField(min, true);
                    EditorGUILayout.PropertyField(max, true);
                }
                continue;
            }

            if (iterator.name == "pierceCountMode")
            {
                EditorGUILayout.PropertyField(iterator, true);

                SerializedProperty mode = iterator;
                SerializedProperty constant = serializedObject.FindProperty("pierceCount");
                SerializedProperty min = serializedObject.FindProperty("pierceCountMin");
                SerializedProperty max = serializedObject.FindProperty("pierceCountMax");

                var selectedMode = (ProjectileTower.ValueMode)mode.enumValueIndex;
                if (selectedMode == ProjectileTower.ValueMode.Constant)
                {
                    EditorGUILayout.PropertyField(constant, true);
                }
                else
                {
                    EditorGUILayout.PropertyField(min, true);
                    EditorGUILayout.PropertyField(max, true);
                }
                continue;
            }

            if (iterator.name == "projectileSizeMultiplierMode")
            {
                EditorGUILayout.PropertyField(iterator, true);

                SerializedProperty mode = iterator;
                SerializedProperty constant = serializedObject.FindProperty("projectileSizeMultiplier");
                SerializedProperty min = serializedObject.FindProperty("projectileSizeMultiplierMin");
                SerializedProperty max = serializedObject.FindProperty("projectileSizeMultiplierMax");

                var selectedMode = (ProjectileTower.ValueMode)mode.enumValueIndex;
                if (selectedMode == ProjectileTower.ValueMode.Constant)
                {
                    EditorGUILayout.PropertyField(constant, true);
                }
                else
                {
                    EditorGUILayout.PropertyField(min, true);
                    EditorGUILayout.PropertyField(max, true);
                }
                continue;
            }

            if (iterator.name == "customColor")
            {
                EditorGUILayout.PropertyField(iterator, true);

                SerializedProperty customColor = iterator;
                if (customColor.boolValue)
                {
                    SerializedProperty colorMode = serializedObject.FindProperty("projectileColorMode");
                    EditorGUILayout.PropertyField(colorMode, true);

                    var selectedMode = (ProjectileTower.ProjectileColorMode)colorMode.enumValueIndex;
                    if (selectedMode == ProjectileTower.ProjectileColorMode.Constant)
                    {
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("projectileConstantColorType"), true);
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("projectileColorMin"), true);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("projectileColorMax"), true);
                    }
                }
                continue;
            }

            if (iterator.name == "bulletSpeed" ||
                iterator.name == "projectileMinSpeed" ||
                iterator.name == "projectileMaxSpeed" ||
                iterator.name == "projectileAcceleration" ||
                iterator.name == "projectileAccelerationMin" ||
                iterator.name == "projectileAccelerationMax" ||
                iterator.name == "pierceCount" ||
                iterator.name == "pierceCountMin" ||
                iterator.name == "pierceCountMax" ||
                iterator.name == "projectileSizeMultiplier" ||
                iterator.name == "projectileSizeMultiplierMin" ||
                iterator.name == "projectileSizeMultiplierMax" ||
                iterator.name == "projectileColorMode" ||
                iterator.name == "projectileConstantColorType" ||
                iterator.name == "projectileColorMin" ||
                iterator.name == "projectileColorMax")
            {
                continue;
            }

            EditorGUILayout.PropertyField(iterator, true);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
