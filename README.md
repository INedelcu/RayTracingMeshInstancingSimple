# RayTracingMeshInstancingSimple
Unity sample project using instancing and per-instance shader properties in Ray Tracing.

## Description
The project uses [RayTracingAccelerationStructure.AddInstances](https://docs.unity3d.com/2023.1/Documentation/ScriptReference/Rendering.RayTracingAccelerationStructure.AddInstances.html) function to add many ray tracing instances of a Mesh to an acceleration structure.

<img src="Images/AddInstances.png" width="1280">

Resource binding for hit shaders is done through [shader tables and shader records](https://microsoft.github.io/DirectX-Specs/d3d/Raytracing.html#shader-record). Writing shader records can be an expensive CPU operation when Materials are complex and use many resources and properties. When using *AddInstances* function, all ray tracing instances associated with the specified Mesh will use the same shader record which can notably improve CPU performance.
