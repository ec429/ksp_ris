LIBDIR := $(HOME)/ksp/KSP_Data/Managed
KSPDTA := $(HOME)/ksp/GameData/
BINDIR := bin
OUTDIR := ../GameData/RIS/Plugins

SOURCES := Core.cs Util.cs MiniJSON.cs UI/MasterWindow.cs UI/AbstractWindow.cs Properties/AssemblyInfo.cs

# Comma-separated list of all the Unity DLLs in KSP 1.8.1
UNITY := $(LIBDIR)/UnityEngine.dll,$(LIBDIR)/UnityEngine.ARModule.dll,$(LIBDIR)/UnityEngine.AssetBundleModule.dll,$(LIBDIR)/UnityEngine.AnimationModule.dll,$(LIBDIR)/UnityEngine.Physics2DModule.dll,$(LIBDIR)/UnityEngine.TLSModule.dll,$(LIBDIR)/UnityEngine.TerrainPhysicsModule.dll,$(LIBDIR)/UnityEngine.SpriteMaskModule.dll,$(LIBDIR)/UnityEngine.ImageConversionModule.dll,$(LIBDIR)/UnityEngine.LocalizationModule.dll,$(LIBDIR)/UnityEngine.WindModule.dll,$(LIBDIR)/UnityEngine.UnityWebRequestWWWModule.dll,$(LIBDIR)/UnityEngine.UnityAnalyticsModule.dll,$(LIBDIR)/UnityEngine.DSPGraphModule.dll,$(LIBDIR)/UnityEngine.GridModule.dll,$(LIBDIR)/UnityEngine.UnityWebRequestAudioModule.dll,$(LIBDIR)/UnityEngine.ParticleSystemModule.dll,$(LIBDIR)/UnityEngine.VFXModule.dll,$(LIBDIR)/UnityEngine.SpriteShapeModule.dll,$(LIBDIR)/UnityEngine.VehiclesModule.dll,$(LIBDIR)/UnityEngine.UnityConnectModule.dll,$(LIBDIR)/UnityEngine.AndroidJNIModule.dll,$(LIBDIR)/UnityEngine.HotReloadModule.dll,$(LIBDIR)/UnityEngine.UnityTestProtocolModule.dll,$(LIBDIR)/UnityEngine.TextRenderingModule.dll,$(LIBDIR)/UnityEngine.UmbraModule.dll,$(LIBDIR)/UnityEngine.UnityWebRequestTextureModule.dll,$(LIBDIR)/UnityEngine.VRModule.dll,$(LIBDIR)/UnityEngine.FileSystemHttpModule.dll,$(LIBDIR)/UnityEngine.AccessibilityModule.dll,$(LIBDIR)/UnityEngine.AudioModule.dll,$(LIBDIR)/UnityEngine.UI.dll,$(LIBDIR)/UnityEngine.DirectorModule.dll,$(LIBDIR)/UnityEngine.JSONSerializeModule.dll,$(LIBDIR)/UnityEngine.UIModule.dll,$(LIBDIR)/UnityEngine.InputModule.dll,$(LIBDIR)/UnityEngine.PerformanceReportingModule.dll,$(LIBDIR)/UnityEngine.ClothModule.dll,$(LIBDIR)/UnityEngine.UnityWebRequestModule.dll,$(LIBDIR)/UnityEngine.ClusterRendererModule.dll,$(LIBDIR)/UnityEngine.GameCenterModule.dll,$(LIBDIR)/UnityEngine.TerrainModule.dll,$(LIBDIR)/UnityEngine.UnityWebRequestAssetBundleModule.dll,$(LIBDIR)/UnityEngine.SubstanceModule.dll,$(LIBDIR)/UnityEngine.TilemapModule.dll,$(LIBDIR)/UnityEngine.InputLegacyModule.dll,$(LIBDIR)/UnityEngine.VideoModule.dll,$(LIBDIR)/UnityEngine.UNETModule.dll,$(LIBDIR)/UnityEngine.CrashReportingModule.dll,$(LIBDIR)/UnityEngine.SharedInternalsModule.dll,$(LIBDIR)/UnityEngine.UIElementsModule.dll,$(LIBDIR)/UnityEngine.XRModule.dll,$(LIBDIR)/UnityEngine.TextCoreModule.dll,$(LIBDIR)/UnityEngine.StreamingModule.dll,$(LIBDIR)/UnityEngine.ProfilerModule.dll,$(LIBDIR)/UnityEngine.CoreModule.dll,$(LIBDIR)/UnityEngine.IMGUIModule.dll,$(LIBDIR)/UnityEngine.ScreenCaptureModule.dll,$(LIBDIR)/UnityEngine.AIModule.dll,$(LIBDIR)/UnityEngine.ClusterInputModule.dll,$(LIBDIR)/UnityEngine.PhysicsModule.dll

all: $(OUTDIR)/ksp_ris.dll

$(BINDIR)/ksp_ris.dll: $(SOURCES)
	mcs -t:library -nostdlib -r:$(LIBDIR)/Assembly-CSharp.dll,$(UNITY),$(LIBDIR)/System.dll,$(LIBDIR)/System.Core.dll,$(LIBDIR)/mscorlib.dll,$(KSPDTA)/ContractConfigurator/ContractConfigurator.dll -out:$@ $(SOURCES)

$(OUTDIR)/ksp_ris.dll: $(BINDIR)/ksp_ris.dll
	cp $< $@

clean:
	-rm $(BINDIR)/ksp_ris.dll $(OUTDIR)/ksp_ris.dll
