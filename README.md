# CSV2UnityMesh

CSV2UnityMesh is a Unity package that allows you to convert CSV data into Unity meshes and FBX files. You can easily integrate it into your Unity project using the Unity Package Manager (UPM).

## Installation

To install CSV2UnityMesh, follow these steps:

1. Open your Unity project.
2. Go to **Window > Package Manager**.
3. Click on the "+" button and select "Add package from disk/url..."

## Usage

1. Drag and drop your CSV file into the Unity Editor.
2. Navigate to **Tools > CSV2UnityMesh** to open the CSV import window.
3. Drag your CSV asset into the CSV2UnityMesh window.
4. Verify the accuracy of each attribute's data source, such as position, normal, and tangent.
5. Choose the output directory and file name for the generated files.
6. Click on "Export" to generate mesh and FBX files at the specified path.

### Note

- The exported FBX file may have incomplete texcoord data because FBX supports only float2 for texcoords. However, mesh files contain complete data, including float4 texcoords, which Unity can handle.
