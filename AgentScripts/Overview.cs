using UnityEngine;

public static class Overview
{
    public static string Main(string path, float x, float y, float size, float pitch, float yaw)
    {
        var go = new GameObject("__OverviewCam");
        var cam = go.AddComponent<Camera>();
        cam.orthographic = size > 0;
        if (size > 0) cam.orthographicSize = size; else cam.fieldOfView = 50;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.36f, 0.5f, 0.68f);
        cam.farClipPlane = 200;
        var rot = Quaternion.Euler(pitch, yaw, 0);
        go.transform.rotation = rot;
        go.transform.position = new Vector3(x, y, 0) - rot * Vector3.forward * (size > 0 ? 40 : Mathf.Abs(size));
        int w = 1600, h = 900;
        var rt = new RenderTexture(w, h, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(go);
        return path;
    }
}
