using UnityEngine;

public class TextureUtil
{
    public static Texture2D Copy(Texture2D tex)
    {
        Texture2D oTex = new Texture2D(tex.width, tex.height);
        oTex.SetPixels32(tex.GetPixels32());
        oTex.Apply();
        return oTex;
    }

    public static Texture2D Copy(WebCamTexture tex)
    {
        int width = tex.width;
        int height = tex.height;
        Color32[] colors = new Color32[tex.width * tex.height];
        RotateImage(tex.videoRotationAngle, tex.GetPixels32(), tex.width, tex.height, ref colors, ref width, ref height);
        Texture2D oTex = new Texture2D(width, height);
        oTex.SetPixels32(colors);
        oTex.Apply();
        return oTex;
    }

    // Rotate the RGB image
    public static void RotateImage(
        int rotAngle, Color32[] rgbArray, int w, int h,
        ref Color32[] oRgbArray, ref int ow, ref int oh)
    {
        if (rotAngle == 180)
        {
            ow = w;
            oh = h;
            for (int y = 0; y < h; ++y)
            {
                int oy = h - 1 - y;
                for (int x = 0; x < w; ++x)
                {
                    int ox = w - 1 - x;
                    int index = y * w + x;
                    int oIndex = oy * w + ox;
                    oRgbArray[oIndex] = rgbArray[index];
                }
            }
        }
        else if (rotAngle == 90 || rotAngle == 270)
        {
            ow = h;
            oh = w;
            for (int y = 0; y < h; ++y)
            {
                int ox = (rotAngle == 90) ? y : h - 1 - y;
                for (int x = 0; x < w; ++x)
                {
                    int oy = (rotAngle == 90) ? w - 1 - x : x;
                    int index = y * w + x;
                    int oIndex = oy * ow + ox;
                    oRgbArray[oIndex] = rgbArray[index];
                }
            }
        }
        else
        {
            ow = w;
            oh = h;
            for (int i = 0; i < w * h; ++i)
            {
                oRgbArray[i] = rgbArray[i];
            }
        }
    }
}
