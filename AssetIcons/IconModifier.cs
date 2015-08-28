using System;

using ICities;
using UnityEngine;
using ColossalFramework.Packaging;
using ColossalFramework.UI;
using System.Collections.Generic;
using System.Reflection;
using System.IO;

namespace AssetIcons
{
    public class IconModifier : LoadingExtensionBase
    {
        Texture2D focusedFilterTexture;

        const int THUMBNAIL_SIZE = 100;

        const int TOOLTIP_WIDTH = 492;
        const int TOOLTIP_HEIGHT = 147;

        public override void OnLevelLoaded(LoadMode mode)
        {
            Debug.Log("IconModifier: Patching icons");
            int patchCount = 0;

            // Load the selection filter LUT texture
            var stream = (UnmanagedMemoryStream)Assembly.GetAssembly(typeof(IconModifier)).GetManifestResourceStream("AssetIcons.Assets.SelectFilter.png");
            var data = new BinaryReader(stream).ReadBytes((int)stream.Length);
            focusedFilterTexture = new Texture2D(1024, 32);
            focusedFilterTexture.LoadImage(data);

            // Build references to the assets for thumbnails and tooltips.
            var thumbnails = new Dictionary<string, Package.Asset>();
            var tooltips = new Dictionary<string, Package.Asset>();

            foreach (Package package in PackageManager.allPackages)
            {
                foreach (Package.Asset asset in package)
                {
                    if (asset.type == UserAssetType.CustomAssetMetaData)
                    {
                        CustomAssetMetaData md = asset.Instantiate<CustomAssetMetaData>();
                        if (md.imageRef != null && !thumbnails.ContainsKey(package.packageName))
                        {
                            md.imageRef.Cache();
                            thumbnails.Add(package.packageName, md.imageRef);
                        }
                        if (md.steamPreviewRef != null && !tooltips.ContainsKey(package.packageName))
                        {
                            md.steamPreviewRef.Cache();
                            tooltips.Add(package.packageName, md.steamPreviewRef);
                        }
                    }
                }
            }

            // Now go through all BuildingInfos and patch their icons.
            int infoCount = PrefabCollection<BuildingInfo>.LoadedCount();
            for (uint infoIndex = 0; infoIndex < infoCount; ++infoIndex)
            {
                BuildingInfo info = PrefabCollection<BuildingInfo>.GetLoaded(infoIndex);
                Texture2D thumbnailTexture = null;
                Texture2D tooltipTexture = null;
                string packageName = info.name.Split('.')[0];

                if (!string.IsNullOrEmpty(info.m_Thumbnail))
                {
                    // This item has a thumbnail, check if it's a generated one.
                    GameObject gameObject = GameObject.Find(info.name);
                    if (gameObject != null)
                    {
                        UIButton uiButton = gameObject.GetComponent<UIButton>();

                        if (uiButton != null && uiButton.atlas != null)
                        {
                            var focusedSpriteInfo = uiButton.atlas[uiButton.focusedFgSprite];
                            if (focusedSpriteInfo != null && focusedSpriteInfo.texture != null)
                            {
                                Color32[] focusedPixels = focusedSpriteInfo.texture.GetPixels32();
                                // The default atlas generator creates ugly dark blue focused icons
                                // by removing everything from the red and green channel.  Detect these
                                // by adding up the amount of red and green in the image.
                                long nonBlueCount = 0;
                                foreach (Color32 pixel in focusedPixels)
                                {
                                    if (pixel.a > 32)
                                    {
                                        nonBlueCount += pixel.r + pixel.g;
                                    }
                                }

                                if (nonBlueCount < 10000)
                                {
                                    // This is a generated atlas.  Replace the focused icon by generating
                                    // a new atlas.  Include the tooltip if there is one.
                                    var thumbnailSpriteInfo = uiButton.atlas[uiButton.normalFgSprite];
                                    if (thumbnailSpriteInfo != null && thumbnailSpriteInfo.texture != null)
                                    {
                                        thumbnailTexture = thumbnailSpriteInfo.texture;
                                        thumbnailTexture.name = info.name + "Icon";
                                    }
                                    var tooltipSpriteInfo = uiButton.atlas["tooltip"];
                                    if (tooltipSpriteInfo != null && thumbnailSpriteInfo.texture != null)
                                    {
                                        tooltipTexture = tooltipSpriteInfo.texture;
                                        tooltipTexture.name = "tooltip";
                                    }
                                }
                            }
                        }
                    }
                }

                // Create a thumbnail based on the steam workshop image.
                if (string.IsNullOrEmpty(info.m_Thumbnail) && thumbnails.ContainsKey(packageName))
                {
                    thumbnailTexture = thumbnails[packageName].Instantiate<Texture2D>();
                    if (thumbnailTexture != null)
                    {
                        thumbnailTexture.wrapMode = TextureWrapMode.Clamp;
                        thumbnailTexture.name = info.name + "Icon";
                        if (thumbnailTexture.width > thumbnailTexture.height)
                        {
                            ScaleTexture(thumbnailTexture, THUMBNAIL_SIZE, (THUMBNAIL_SIZE * thumbnailTexture.height) / thumbnailTexture.width);
                        }
                        else
                        {
                            ScaleTexture(thumbnailTexture, (THUMBNAIL_SIZE * thumbnailTexture.width) / thumbnailTexture.height, THUMBNAIL_SIZE);
                        }
                    }
                }

                // Create a tooltip image based on the steam workshop image.
                if (string.IsNullOrEmpty(info.m_InfoTooltipThumbnail) && tooltips.ContainsKey(packageName))
                {
                    tooltipTexture = tooltips[packageName].Instantiate<Texture2D>();
                    if (tooltipTexture != null)
                    {
                        // The tooltip texture name must match the info name, as that's the key value that's used
                        // (stored on UIButton.m_Tooltip, not by us).
                        tooltipTexture.name = info.name;

                        // Crop and scale the tooltip to TOOLTIP_WIDTH x TOOLTIP_HEIGHT
                        if (((float)tooltipTexture.width / (float)tooltipTexture.height) > (float)TOOLTIP_WIDTH/(float)TOOLTIP_HEIGHT)
                        {
                            // Picture is too wide, scale to TOOLTIP_HEIGHT pixels tall and then crop out the middle
                            ScaleTexture(tooltipTexture, (TOOLTIP_HEIGHT * tooltipTexture.width) / tooltipTexture.height, TOOLTIP_HEIGHT);
                            CropTexture(tooltipTexture, (tooltipTexture.width - TOOLTIP_WIDTH) / 2, 0, TOOLTIP_WIDTH, TOOLTIP_HEIGHT);
                        }
                        else if (((float)tooltipTexture.width / (float)tooltipTexture.height) < (float)TOOLTIP_WIDTH/(float)TOOLTIP_HEIGHT)
                        {
                            // Picture is too tall, scale to TOOLTIP_WIDTH pixels wide and then crop out the middle
                            ScaleTexture(tooltipTexture, TOOLTIP_WIDTH, (TOOLTIP_WIDTH * tooltipTexture.height) / tooltipTexture.width);
                            CropTexture(tooltipTexture, 0, (tooltipTexture.height - TOOLTIP_HEIGHT) / 2, TOOLTIP_WIDTH, TOOLTIP_HEIGHT);
                        }
                        else
                        {
                            // Picture is the right aspect ratio, just scale it
                            ScaleTexture(tooltipTexture, TOOLTIP_WIDTH, TOOLTIP_HEIGHT);
                        }
                    }
                }

                // Build these textures into an atlas.
                Texture2D[] textures;
                if (thumbnailTexture != null)
                {
                    if (tooltipTexture != null)
                    {
                        textures = new Texture2D[] { thumbnailTexture, null, null, null, null, tooltipTexture };
                    }
                    else
                    {
                        textures = new Texture2D[] { thumbnailTexture, null, null, null, null };
                    }
                    GenerateMissingThumbnailVariants(ref textures);
                }
                else if (tooltipTexture != null)
                {
                    textures = new Texture2D[] { tooltipTexture };
                }
                else
                {
                    // Hmm, we've failed to make any textures.  Move on to the next BuildingInfo.
                    continue;
                }

                UITextureAtlas atlas = AssetImporterThumbnails.CreateThumbnailAtlas(textures, info.name + "Atlas");

                if (thumbnailTexture != null)
                {
                    // Store the thumbnail atlas and icon names on the uiButton for this building.
                    GameObject gameObject = GameObject.Find(info.name);
                    if (gameObject != null)
                    {
                        UIButton uiButton = gameObject.GetComponent<UIButton>();

                        if (uiButton != null)
                        {
                            uiButton.atlas = atlas;

                            string baseIconName = info.name + "Icon";
                            uiButton.normalFgSprite = baseIconName;
                            uiButton.focusedFgSprite = baseIconName + "Focused";
                            uiButton.hoveredFgSprite = baseIconName + "Hovered";
                            uiButton.pressedFgSprite = baseIconName + "Pressed";
                            uiButton.disabledFgSprite = baseIconName + "Disabled";
                        }
                    }
                }

                if (tooltipTexture != null)
                {
                    // Store the tooltip atlas for this building.
                    info.m_InfoTooltipAtlas = atlas;

                    // This is actually the transition thumbnail, we set it to the default to blank
                    // out the tooltip during transitions, which matches the behaviour of the
                    // existing icons.
                    info.m_InfoTooltipThumbnail = "ThumbnailBuildingDefault";
                }

                ++patchCount;
            }

            Debug.Log(String.Format("IconModifier: Complete.  Patched {0} icons.", patchCount));
        }

        public static void ScaleTexture(Texture2D tex, int width, int height)
        {
            var newPixels = new Color[width * height];
            for (int y = 0; y < height; ++y)
            {
                for (int x = 0; x < width; ++x)
                {
                    newPixels[y * width + x] = tex.GetPixelBilinear(((float)x) / width, ((float)y) / height);
                }
            }
            tex.Resize(width, height);
            tex.SetPixels(newPixels);
            tex.Apply();
        }

        public static void CropTexture(Texture2D tex, int x, int y, int width, int height)
        {
            var newPixels = tex.GetPixels(x, y, width, height);
            tex.Resize(width, height);
            tex.SetPixels(newPixels);
            tex.Apply();
        }

        // Colorize the focused icon blue using the LUT texture
        // Use a border of 8 (256/32) to ensure we don't pick up neighboring patches
        Color32 ColorizeFocused(Color32 c)
        {
            int b = c.b * 31 / 255;
            float u = ((8f + (float)c.r) / 271) / 32 + ((float)b / 32);
            float v = 1f - ((8f + (float)c.g) / 271);
            Color32 result =  focusedFilterTexture.GetPixelBilinear(u, v);
            result.a = c.a;
            return result;
        }

        // Our own version of this as the one in AssetImporterThumbnails has hardcoded dimensions
        // and generates ugly dark blue focused thumbnails.
        public void GenerateMissingThumbnailVariants(ref Texture2D[] thumbs)
        {
            if (thumbs[0] == null)
            {
                return;
            }
            Texture2D baseTexture = thumbs[0];
            Texture2D focusedTexture = thumbs[1];
            Texture2D hoveredTexture = thumbs[2];
            Texture2D pressedTexture = thumbs[3];
            Texture2D disabledTexture = thumbs[4];
            var newPixels = new Color32[baseTexture.width * baseTexture.height];
            var pixels = baseTexture.GetPixels32();
            if (focusedTexture == null)
            {
                ApplyFilter(pixels, newPixels, ColorizeFocused);
                focusedTexture = new Texture2D(baseTexture.width, baseTexture.height, TextureFormat.ARGB32, false, false);
                focusedTexture.SetPixels32(newPixels);
                focusedTexture.Apply(false);
            }
            focusedTexture.name = baseTexture.name + "Focused";
            if (hoveredTexture == null)
            {
                ApplyFilter(pixels, newPixels, c => new Color32((byte)(128 + c.r / 2), (byte)(128 + c.g / 2), (byte)(128 + c.b / 2), c.a));
                hoveredTexture = new Texture2D(baseTexture.width, baseTexture.height, TextureFormat.ARGB32, false, false);
                hoveredTexture.SetPixels32(newPixels);
                hoveredTexture.Apply(false);
            }
            hoveredTexture.name = baseTexture.name + "Hovered";
            if (pressedTexture == null)
            {
                ApplyFilter(pixels, newPixels, c => new Color32((byte)(192 + c.r / 4), (byte)(192 + c.g / 4), (byte)(192 + c.b / 4), c.a));
                pressedTexture = new Texture2D(baseTexture.width, baseTexture.height, TextureFormat.ARGB32, false, false);
                pressedTexture.SetPixels32(newPixels);
                pressedTexture.Apply(false);
            }
            pressedTexture.name = baseTexture.name + "Pressed";
            if (disabledTexture == null)
            {
                ApplyFilter(pixels, newPixels, c => new Color32(0, 0, 0, c.a));
                disabledTexture = new Texture2D(baseTexture.width, baseTexture.height, TextureFormat.ARGB32, false, false);
                disabledTexture.SetPixels32(newPixels);
                disabledTexture.Apply(false);
            }
            disabledTexture.name = baseTexture.name + "Disabled";
            thumbs[1] = focusedTexture;
            thumbs[2] = hoveredTexture;
            thumbs[3] = pressedTexture;
            thumbs[4] = disabledTexture;
        }

        delegate Color32 Filter(Color32 c);

        private static void ApplyFilter(Color32[] src, Color32[] dst, Filter filter)
        {
            for (int i = 0; i < src.Length; i++)
            {
                dst[i] = filter(src[i]);
            }
        }
    }
}
