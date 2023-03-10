namespace FastMobilePostProcessing
{
    using UnityEngine;
    using UnityStandardAssets.ImageEffects;

    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("FastMobilePostProcessing/FastMobileBloom")]
    public class FastMobileBloomDOF : PostEffectsBase
    {
        public bool EnableBloom = true;
        public bool EnableDOF = true;
        public bool EnableRadialBlur = true;

        [Range(0.25f, 5.5f)]
        public float BlurSize = 1.0f;
        [Range(1, 4)]
        public int BlurIterations = 2;
        [Range(0.0f, 1.5f)]
        public float bloomThreshold = 0.25f;
        [Range(0.0f, 2.5f)]
        public float BloomIntensity = 1.0f;

        public Transform DOFFocalTransform;             // 聚焦物体
        public float DOFFocalLength = 3f;               // 焦点距离（焦点到相机的距离）
        public float DOFFocalSize = 0.2f;               // 景深大小
        public float DOFAperture = 2f;                  // 光圈（景深系数，光圈越大景深越浅）

        [Range(0.0f, 1.0f)]
        public float RadialBlurCenterX = 0.5f;          // 径向模糊中心屏幕横坐标
        [Range(0.0f, 1.0f)]
        public float RadialBlurCenterY = 0.5f;          // 径向模糊中心屏幕纵坐标
        [Range(-0.5f, 0.5f)]
        public float RadialBlurSampleDistance = 0.1f;   // 径向模糊采样距离
        [Range(0.0f, 8.0f)]
        public float RadialBlurStrength = 3.0f;         // 径向模糊强度

        Camera _camera;
        Shader _shader;
        Material _material;

        void OnEnable()
        {
            _camera = GetComponent<Camera>();
        }

        void OnDisable()
        {
            if (_material)
            {
                DestroyImmediate(_material);
            }
        }

        public override bool CheckResources()
        {
            if (!EnableBloom && !EnableDOF && !(EnableRadialBlur && RadialBlurStrength > 0f))
                return false;

            CheckSupport(EnableDOF);

            if (_shader == null)
            {
                _shader = Shader.Find("Hidden/FastMobilePostProcessing");
            }
            _material = CheckShaderAndCreateMaterial(_shader, _material);

            if (!isSupported)
            {
                ReportAutoDisable();
            }

            return isSupported;
        }

        void OnRenderImage(RenderTexture sourceRT, RenderTexture destinationRT)
        {
            if (!CheckResources())
            {
                Graphics.Blit(sourceRT, destinationRT);
                return;
            }

            _camera.depthTextureMode = EnableDOF ? DepthTextureMode.Depth : DepthTextureMode.None;

            RenderTexture blurredRT = null;
            RenderTexture radialBlurredRT = null;
            RenderTexture srcRT = sourceRT;
            RenderTexture destRT = destinationRT;

            if (EnableBloom || EnableDOF)
            {
                // Initial downsample
                blurredRT = RenderTexture.GetTemporary(sourceRT.width / 4, sourceRT.height / 4, 0, sourceRT.format);
                blurredRT.filterMode = FilterMode.Bilinear;

                _material.SetFloat("_BlurSize", BlurSize);
                if (EnableBloom)
                {
                    _material.SetFloat("_BloomThreshold", bloomThreshold);
                    _material.SetFloat("_BloomIntensity", BloomIntensity);
                }
                Graphics.Blit(sourceRT, blurredRT, _material, 0);

                // Downscale
                for (int i = 0; i < BlurIterations - 1; ++i)
                {
                    RenderTexture blurredRT2 = RenderTexture.GetTemporary(blurredRT.width / 2, blurredRT.height / 2, 0, sourceRT.format);
                    blurredRT2.filterMode = FilterMode.Bilinear;

                    Graphics.Blit(blurredRT, blurredRT2, _material, 0);

                    RenderTexture.ReleaseTemporary(blurredRT);
                    blurredRT = blurredRT2;
                }
                // Upscale
                for (int i = 0; i < BlurIterations - 1; ++i)
                {
                    RenderTexture blurredRT2 = RenderTexture.GetTemporary(blurredRT.width * 2, blurredRT.height * 2, 0, sourceRT.format);
                    blurredRT2.filterMode = FilterMode.Bilinear;

                    Graphics.Blit(blurredRT, blurredRT2, _material, 1);

                    RenderTexture.ReleaseTemporary(blurredRT);
                    blurredRT = blurredRT2;
                }

                _material.SetTexture("_BlurredTex", blurredRT);

                if (EnableRadialBlur && RadialBlurStrength > 0f)
                {
                    destRT = RenderTexture.GetTemporary(sourceRT.width, sourceRT.height, 0, sourceRT.format);
                    destRT.filterMode = FilterMode.Bilinear;

                    srcRT = destRT;
                }
            }

            if (EnableBloom)
            {
                _material.EnableKeyword("BLOOM_ON");
            }
            else
            {
                _material.DisableKeyword("BLOOM_ON");
            }
            if (EnableDOF)
            {
                if (DOFFocalTransform != null)
                {
                    DOFFocalLength = _camera.WorldToScreenPoint(DOFFocalTransform.position).z;
                }
                _material.SetFloat("_FocalLength", DOFFocalLength / _camera.farClipPlane);
                _material.SetFloat("_FocalSize", DOFFocalSize);
                _material.SetFloat("_Aperture", DOFAperture);
                _material.EnableKeyword("DOF_ON");
            }
            else
            {
                _material.DisableKeyword("DOF_ON");
            }

            Graphics.Blit(sourceRT, destRT, _material, 3);

            RenderTexture.ReleaseTemporary(blurredRT);

            if (EnableRadialBlur && RadialBlurStrength > 0f)
            {
                radialBlurredRT = RenderTexture.GetTemporary(srcRT.width / 2, srcRT.height / 2, 0, srcRT.format);
                radialBlurredRT.filterMode = FilterMode.Bilinear;

                _material.SetFloat("_RadialBlurCenterX", RadialBlurCenterX);
                _material.SetFloat("_RadialBlurCenterY", RadialBlurCenterY);
                _material.SetFloat("_RadialBlurSampleDistance", RadialBlurSampleDistance);
                _material.SetFloat("_RadialBlurStrength", RadialBlurStrength);

                Graphics.Blit(srcRT, radialBlurredRT, _material, 2);

                _material.SetTexture("_RadialBlurredTex", radialBlurredRT);

                Graphics.Blit(srcRT, destinationRT, _material, 4);

                srcRT = null;
                RenderTexture.ReleaseTemporary(destRT);
                RenderTexture.ReleaseTemporary(radialBlurredRT);
            }
        }
    }
}
