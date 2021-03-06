// This file is a part of MPDN Extensions.
// https://github.com/zachsaw/MPDN_Extensions
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public
// License along with this library.

using System;
using Mpdn.Extensions.Framework.RenderChain;
using Mpdn.RenderScript;
using SharpDX;

namespace Mpdn.Extensions.RenderScripts
{
    namespace Shiandow.Nedi
    {
        public class Nedi : RenderChain
        {
            #region Settings

            public Nedi()
            {
                AlwaysDoubleImage = false;
                ForceCentered = false;
            }

            public bool AlwaysDoubleImage { get; set; }
            public bool ForceCentered { get; set; }

            #endregion

            public float[] LumaConstants = {0.2126f, 0.7152f, 0.0722f};

            private bool UseNedi(ITextureFilter input)
            {
                var size = input.Size();
                if (size.IsEmpty)
                    return false;

                if (AlwaysDoubleImage)
                    return true;

                return Renderer.TargetSize.Width > size.Width ||
                       Renderer.TargetSize.Height > size.Height;
            }

            protected override ITextureFilter CreateFilter(ITextureFilter input)
            {
                Func<TextureSize, TextureSize> transformWidth = s => new TextureSize(2*s.Width, s.Height);
                Func<TextureSize, TextureSize> transformHeight = s => new TextureSize(s.Width, 2*s.Height);

                var nedi1Shader = CompileShader("NEDI-I.hlsl").Configure(arguments: LumaConstants);
                var nedi2Shader = CompileShader("NEDI-II.hlsl").Configure(arguments: LumaConstants);
                var nediHInterleaveShader = CompileShader("NEDI-HInterleave.hlsl").Configure(transform: transformWidth);
                var nediVInterleaveShader = CompileShader("NEDI-VInterleave.hlsl").Configure(transform: transformHeight);

                if (!UseNedi(input))
                    return input;

                var nedi1 = nedi1Shader.ApplyTo(input);
                var nediH = nediHInterleaveShader.ApplyTo(input, nedi1);
                var nedi2 = nedi2Shader.ApplyTo(nediH);
                var nediV = nediVInterleaveShader.ApplyTo(nediH, nedi2);

                return nediV.Convolve(ForceCentered ? Renderer.LumaUpscaler : null, offset: new Vector2(0.5f, 0.5f));
            }
        }

        public class NediScaler : RenderChainUi<Nedi, NediConfigDialog>
        {
            protected override string ConfigFileName
            {
                get { return "Shiandow.Nedi"; }
            }

            public override string Category
            {
                get { return "Upscaling"; }
            }

            public override ExtensionUiDescriptor Descriptor
            {
                get
                {
                    return new ExtensionUiDescriptor
                    {
                        Guid = new Guid("B8E439B7-7DC2-4FC1-94E2-608A39756FB0"),
                        Name = "NEDI",
                        Description = GetDescription(),
                        Copyright = "NEDI by Shiandow",
                    };
                }
            }

            private string GetDescription()
            {
                var options = string.Format("{0}", Settings.AlwaysDoubleImage ? " (forced)" : string.Empty);
                return string.Format("NEDI image doubler{0}", options);
            }
        }
    }
}
