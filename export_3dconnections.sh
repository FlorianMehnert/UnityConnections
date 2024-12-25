#!/bin/bash

# Path to the Unity Editor installed by Unity Hub
UNITY_PATH="/home/florian/Unity/Hub/Editor/6000.0.25f1/Editor/Unity"

# Path to your Unity project
PROJECT_PATH="/home/florian/RiderProjects/UnityConnections/Assets/3DConnections/"

# Unity Editor method to execute
EXECUTE_METHOD="PackageExporter.ExportPackage"

# Run Unity in batch mode
$UNITY_PATH -batchmode -quit -projectPath "$PROJECT_PATH" -executeMethod "$EXECUTE_METHOD"

if [ $? -eq 0 ]; then
    echo "Unity package exported successfully."
else
    echo "Failed to export Unity package."
    exit 1
fi
