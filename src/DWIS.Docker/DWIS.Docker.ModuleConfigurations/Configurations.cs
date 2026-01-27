
//C# DWIS.Docker.ModuleConfigurations\Configurations.cs
using System.Reflection;

namespace DWIS.Docker.ModuleConfigurations
{
    public class ComposerConfiguration
    {
        public TimeSpan LoopDuration { get; set; } = TimeSpan.FromSeconds(1.0);
        public string? OPCUAURL { get; set; } = "opc.tcp://localhost:48030";
        public TimeSpan ControllerObsolescence { get; set; } = TimeSpan.FromSeconds(5.0);
        public TimeSpan ProcedureObsolescence { get; set; } = TimeSpan.FromSeconds(5.0);
        public TimeSpan FaultDetectionIsolationAndRecoveryObsolescence { get; set; } = TimeSpan.FromSeconds(5.0);
        public TimeSpan SafeOperatingEnvelopeObsolescence { get; set; } = TimeSpan.FromSeconds(5.0);

    }

    public class MicroStateInterpretationConfiguration
    {
        public TimeSpan LoopDuration { get; set; } = TimeSpan.FromSeconds(1.0);
        public string? OPCUAURL { get; set; } = "opc.tcp://localhost:48030";

        public double DefaultProbability { get; set; } = 0.1;

        public double DefaultStandardDeviation { get; set; } = 0.1;

        public int CircularBufferSize { get; set; } = 300;

        public TimeSpan CalibrationMinTimeWindow { get; set; } = TimeSpan.FromSeconds(120);

        public double CalibrationTimeWindowFactor { get; set; } = 0.5;

        public double CalibrationConvergenceTolerance { get; set; } = 1e-6;

        public int CalibrationMaxNumberOfIterations { get; set; } = 1000;

        public bool GenerateRandomValues { get; set; } = false;

    }

    public class MicroStateThresholdsConfiguration
    {
        public static double ZeroAxialVelocityTopOfStringThresholdDefault = 0.1 / 3600.0;
        public static double StableAxialVelocityTopOfStringThresholdDefault = 0.5 / 3600.0;
        public static double ZeroRotationalVelocityTopOfStringThresholdDefault = 0.1 * 2.0 * Math.PI / 60.0;
        public static double StableRotationalVelocityTopOfStringThresholdDefault = 0.5 * 2.0 * Math.PI / 60.0;
        public static double ZeroFlowTopOfStringThresholdDefault = 2.0 / 60000.0;
        public static double StableFlowTopOfStringThresholdDefault = 10.0 / 60000.0;
        public static double ZeroTensionTopOfStringThresholdDefault = 500.0;
        public static double StableTensionTopOfStringThresholdDefault = 1000.0;
        public static double ZeroPressureTopOfStringThresholdDefault = 0.1 * 1e5;
        public static double StablePressureTopOfStringThresholdDefault = 0.1 * 1e5;
        public static double ZeroTorqueTopOfStringThresholdDefault = 2.0;
        public static double StableTorqueTopOfStringThresholdDefault = 10.0;
        public static double ZeroFlowAnnulusOutletThresholdDefault = 1.0 / 60000.0;
        public static double StableFlowAnnulusOutletThresholdDefault = 1.0 / 60000.0;
        public static double ZeroBottomOfStringRockForceThresholdDefault = 200.0;
        public static double StableBottomOfStringRockForceThresholdDefault = 1000.0;
        public static double ZeroRotationalVelocityBottomOfStringThresholdDefault = 0.2 * 2.0 * Math.PI / 60.0;
        public static double StableRotationalVelocityBottomOfStringThresholdDefault = 1.0 * 2.0 * Math.PI / 60.0;
        public static double ZeroAxialVelocityBottomOfStringThresholdDefault = 0.1 / 3600.0;
        public static double StableAxialVelocityBottomOfStringThresholdDefault = 0.1 / 3600.0;
        public static double ZeroFlowBottomOfStringThresholdDefault = 1.0 / 60000.0;
        public static double StableFlowBottomOfStringThresholdDefault = 1.0 / 60000.0;
        public static double ZeroFlowHoleOpenerThresholdDefault = 1.0 / 60000.0;
        public static double StableFlowHoleOpenerThresholdDefault = 1.0 / 60000.0;
        public static double ZeroHoleOpenerOnRockForceThresholdDefault = 500.0;
        public static double MinimumPressureFloatValveDefault = 0.1 * 1e5;
        public static double ZeroFlowBoosterPumpThresholdDefault = 1.0 / 60000.0;
        public static double StableFlowBoosterPumpThresholdDefault = 5.0 / 60000.0;
        public static double ZeroFlowBackPressurePumpThresholdDefault = 1.0 / 60000.0;
        public static double StableFlowBackPressurePumpThresholdDefault = 5.0 / 60000.0;
        public static double MinimumDifferentialPressureRCDSealingThresholdDefault = 0.1 * 1e5;
        public static double MinimumDifferentialPressureSealBalanceThresholdDefault = 0.5 * 1e5;
        public static double ZeroFlowFillPumpDGDThresholdDefault = 1.0 / 60000.0;
        public static double StableFlowFillPumpDGDThresholdDefault = 5.0 / 60000.0;
        public static double ZeroFlowLiftPumpDGDThresholdDefault = 1.0 / 60000.0;
        public static double StableFlowLiftPumpDGDThresholdDefault = 5.0 / 60000.0;
        public static double ZeroCuttingsFlowAnnulusOutletThresholdDefault = 0.1 / 60000.0;
        public static double ZeroCuttingsFlowBottomHoleThresholdDefault = 0.1 / 60000.0;
        public static double ZeroCuttingsFlowTopOfRatHoleThresholdDefault = 0.1 / 60000.0;
        public static double HardStringerThresholdDefault = 60e6;
        public static double ChangeOfFormationUCSSlopeThresholdDefault = 2.5e6;
        public static double ForceOnLedgeThresholdDefault = 10000;
        public static double ForceOnCuttingsBedThresholdDefault = 10000;
        public static double ForceDifferentialStickingThresholdDefault = 10000;
        public static double FluidFlowFormationThresholdDefault = 10.0 / 60000.0;
        public static double FlowCavingsFromFormationThresholdDefault = 0.1 / 60000.0;
        public static double WhirlRateBottomOfStringThresholdDefault = 0.5 * 2.0 * Math.PI / 60.0;
        public static double WhirlRateHoleOpenerThresholdDefault = 0.5 * 2.0 * Math.PI / 60.0;
        public static double WhirlRateDrillStringThresholdDefault = 0.5 * 2.0 * Math.PI / 60.0;
        public static double PowerHFTOThresholdDefault = 1;
        public static double LateralShockRateBHAThresholdDefault = 0.1;
        public static double LateralShockRateDrillStringThresholdDefault = 0.1;
        public static double PeakToPeakAxialOscillationsThresholdDefault = 0.1;
        public static double PeakToPeakTorsionalOscillationsThresholdDefault = 0.5 * 2.0 * Math.PI / 60.0;
        public static double AxialStickDurationThresholdDefault = 1.0;
        public static double TorsionalStickDurationThresholdDefault = 1.0;
        public static double FlowPipeToAnnulusThresholdDefault = 1.0 / 60000.0;
        public static double AtDrillHeightThresholdDefault = 0.01;
        public static double AtStickUpHeightThresholdDefault = 0.01;
        public static double TorqueGradientThresholdDefault = 0.1;
        public static double AnnulusPressureGradientThresholdDefault = 0.1 * 1e5;
        public static double StringPressureGradientThresholdDefault = 0.1 * 1e5;

        public TimeSpan LoopDuration { get; set; } = TimeSpan.FromSeconds(10.0);
        public string? OPCUAURL { get; set; } = "opc.tcp://localhost:48030";

        public double ZeroAxialVelocityTopOfStringThreshold { get; set; } = ZeroAxialVelocityTopOfStringThresholdDefault;
        public double StableAxialVelocityTopOfStringThreshold { get; set; } = StableAxialVelocityTopOfStringThresholdDefault;
        public double ZeroRotationalVelocityTopOfStringThreshold { get; set; } = ZeroRotationalVelocityTopOfStringThresholdDefault;
        public double StableRotationalVelocityTopOfStringThreshold { get; set; } = StableRotationalVelocityTopOfStringThresholdDefault;
        public double ZeroFlowTopOfStringThreshold { get; set; } = ZeroFlowTopOfStringThresholdDefault;
        public double StableFlowTopOfStringThreshold { get; set; } = StableFlowTopOfStringThresholdDefault;
        public double ZeroTensionTopOfStringThreshold { get; set; } = ZeroTensionTopOfStringThresholdDefault;
        public double StableTensionTopOfStringThreshold { get; set; } = StableTensionTopOfStringThresholdDefault;
        public double ZeroPressureTopOfStringThreshold { get; set; } = ZeroPressureTopOfStringThresholdDefault;
        public double StablePressureTopOfStringThreshold { get; set; } = StablePressureTopOfStringThresholdDefault;
        public double ZeroTorqueTopOfStringThreshold { get; set; } = ZeroTorqueTopOfStringThresholdDefault;
        public double StableTorqueTopOfStringThreshold { get; set; } = StableTorqueTopOfStringThresholdDefault;
        public double ZeroFlowAnnulusOutletThreshold { get; set; } = ZeroFlowAnnulusOutletThresholdDefault;
        public double StableFlowAnnulusOutletThreshold { get; set; } = StableFlowAnnulusOutletThresholdDefault;
        public double ZeroBottomOfStringRockForceThreshold { get; set; } = ZeroBottomOfStringRockForceThresholdDefault;
        public double StableBottomOfStringRockForceThreshold { get; set; } = StableBottomOfStringRockForceThresholdDefault;
        public double ZeroRotationalVelocityBottomOfStringThreshold { get; set; } = ZeroRotationalVelocityBottomOfStringThresholdDefault;
        public double StableRotationalVelocityBottomOfStringThreshold { get; set; } = StableRotationalVelocityBottomOfStringThresholdDefault;
        public double ZeroAxialVelocityBottomOfStringThreshold { get; set; } = ZeroAxialVelocityBottomOfStringThresholdDefault;
        public double StableAxialVelocityBottomOfStringThreshold { get; set; } = StableAxialVelocityBottomOfStringThresholdDefault;
        public double ZeroFlowBottomOfStringThreshold { get; set; } = ZeroFlowBottomOfStringThresholdDefault;
        public double StableFlowBottomOfStringThreshold { get; set; } = StableFlowBottomOfStringThresholdDefault;
        public double ZeroFlowHoleOpenerThreshold { get; set; } = ZeroFlowHoleOpenerThresholdDefault;
        public double StableFlowHoleOpenerThreshold { get; set; } = StableFlowHoleOpenerThresholdDefault;
        public double ZeroHoleOpenerOnRockForceThreshold { get; set; } = ZeroHoleOpenerOnRockForceThresholdDefault;
        public double MinimumPressureFloatValve { get; set; } = MinimumPressureFloatValveDefault;
        public double ZeroFlowBoosterPumpThreshold { get; set; } = ZeroFlowBoosterPumpThresholdDefault;
        public double StableFlowBoosterPumpThreshold { get; set; } = StableFlowBoosterPumpThresholdDefault;
        public double ZeroFlowBackPressurePumpThreshold { get; set; } = ZeroFlowBackPressurePumpThresholdDefault;
        public double StableFlowBackPressurePumpThreshold { get; set; } = StableFlowBackPressurePumpThresholdDefault;
        public double MinimumDifferentialPressureRCDSealingThreshold { get; set; } = MinimumDifferentialPressureRCDSealingThresholdDefault;
        public double MinimumDifferentialPressureSealBalanceThreshold { get; set; } = MinimumDifferentialPressureSealBalanceThresholdDefault;
        public double ZeroFlowFillPumpDGDThreshold { get; set; } = ZeroFlowFillPumpDGDThresholdDefault;
        public double StableFlowFillPumpDGDThreshold { get; set; } = StableFlowFillPumpDGDThresholdDefault;
        public double ZeroFlowLiftPumpDGDThreshold { get; set; } = ZeroFlowLiftPumpDGDThresholdDefault;
        public double StableFlowLiftPumpDGDThreshold { get; set; } = StableFlowLiftPumpDGDThresholdDefault;
        public double ZeroCuttingsFlowAnnulusOutletThreshold { get; set; } = ZeroCuttingsFlowAnnulusOutletThresholdDefault;
        public double ZeroCuttingsFlowBottomHoleThreshold { get; set; } = ZeroCuttingsFlowBottomHoleThresholdDefault;
        public double ZeroCuttingsFlowTopOfRatHoleThreshold { get; set; } = ZeroCuttingsFlowTopOfRatHoleThresholdDefault;
        public double HardStringerThreshold { get; set; } = HardStringerThresholdDefault;
        public double ChangeOfFormationUCSSlopeThreshold { get; set; } = ChangeOfFormationUCSSlopeThresholdDefault;
        public double ForceOnLedgeThreshold { get; set; } = ForceOnLedgeThresholdDefault;
        public double ForceOnCuttingsBedThreshold { get; set; } = ForceOnCuttingsBedThresholdDefault;
        public double ForceDifferentialStickingThreshold { get; set; } = ForceDifferentialStickingThresholdDefault;
        public double FluidFlowFormationThreshold { get; set; } = FluidFlowFormationThresholdDefault;
        public double FlowCavingsFromFormationThreshold { get; set; } = FlowCavingsFromFormationThresholdDefault;
        public double WhirlRateBottomOfStringThreshold { get; set; } = WhirlRateBottomOfStringThresholdDefault;
        public double WhirlRateHoleOpenerThreshold { get; set; } = WhirlRateHoleOpenerThresholdDefault;
        public double WhirlRateDrillStringThreshold { get; set; } = WhirlRateDrillStringThresholdDefault;
        public double PowerHFTOThreshold { get; set; } = PowerHFTOThresholdDefault;
        public double LateralShockRateBHAThreshold { get; set; } = LateralShockRateBHAThresholdDefault;
        public double LateralShockRateDrillStringThreshold { get; set; } = LateralShockRateDrillStringThresholdDefault;
        public double PeakToPeakAxialOscillationsThreshold { get; set; } = PeakToPeakAxialOscillationsThresholdDefault;
        public double PeakToPeakTorsionalOscillationsThreshold { get; set; } = PeakToPeakTorsionalOscillationsThresholdDefault;
        public double AxialStickDurationThreshold { get; set; } = AxialStickDurationThresholdDefault;
        public double TorsionalStickDurationThreshold { get; set; } = TorsionalStickDurationThresholdDefault;
        public double FlowPipeToAnnulusThreshold { get; set; } = FlowPipeToAnnulusThresholdDefault;
        public double AtDrillHeightThreshold { get; set; } = AtDrillHeightThresholdDefault;
        public double AtStickUpHeightThreshold { get; set; } = AtStickUpHeightThresholdDefault;
        public double TorqueGradientThreshold { get; set; } = TorqueGradientThresholdDefault;
        public double AnnulusPressureGradientThreshold { get; set; } = AnnulusPressureGradientThresholdDefault;
        public double StringPressureGradientThreshold { get; set; } = StringPressureGradientThresholdDefault;
    }

    public class GenericBridgeConfiguration
    {
        public static TimeSpan MaxHeartBeatRefreshDelayDefault = TimeSpan.FromSeconds(5.0);
        public TimeSpan LoopDuration { get; set; } = TimeSpan.FromSeconds(1.0);
        public string? GeneralBlackboard { get; set; } = "opc.tcp://localhost:48030";

        public string? LocalBlackboard { get; set; } = "opc.tcp://localhost:48031";

        public TimeSpan MaxHeartBeatRefreshDelay { get; set; } = MaxHeartBeatRefreshDelayDefault;
    }

    public class AutoDrillerConfiguration : GenericBridgeConfiguration
    {
        public static double FlowrateSetPointROCDefault = 20.0 / 60000.0;
        public static double FlowrateLimitROCDefault = 20.0 / 60000.0;
        public static double RotationalSpeedSetPointROCDefault = 20.0 / 60.0;
        public static double RotationalSpeedLimitROCDefault = 20.0 / 60.0;
        public static double ROPLimitROCDefault = 5.0 / 3600.0;
        public static double WOBLimitROCDefault = 10000;
        public static double TOBLimitROCDefault = 500.0;
        public static double DPLimitROCDefault = 1e5;
        public static double LowestDrillHeightDefault = 1.5;
        public static double DrillOffThresholdDefault = 500;
        public static double WOBPIControllerProportionalGainDefault = 0.08;
        public static double WOBPIControllerIntegralGainDefault = 0.0002;

        public double FlowrateSetPointROC { get; set; } = FlowrateSetPointROCDefault;
        public double FlowrateLimitROC { get; set; } = FlowrateLimitROCDefault;
        public double RotationalSpeedSetPointROC { get; set; } = RotationalSpeedSetPointROCDefault;
        public double RotationalSpeedLimitROC { get; set; } = RotationalSpeedLimitROCDefault;
        public double ROPLimitROC { get; set; } = ROPLimitROCDefault;
        public double WOBLimitROC { get; set; } = WOBLimitROCDefault;
        public double TOBLimitROC { get; set; } = TOBLimitROCDefault;
        public double DPLimitROC { get; set; } = DPLimitROCDefault;
        public double LowestDrillHeight { get; set; } = LowestDrillHeightDefault;
        public double DrillOffThreshold { get; set; } = DrillOffThresholdDefault;
        public double WOBPIControllerProportionalGain { get; set; } = WOBPIControllerProportionalGainDefault;
        public double WOBPIControllerIntegralGain { get; set; } = WOBPIControllerIntegralGainDefault;
    }

    // Added configurations copied from bridge projects and renamed to match the original project names.
    // Source: DWIS.ADCSBridge.Generic.FrictionTest.Configuration
    public class FrictionTestConfiguration
    {
        public static double StickUpHeightDefault = 0.8;
        public static double LowestDrillElevationDefault = 0.2;

        public bool UseSimpleMode { get; set; } = false;
        public double? StickUpHeight { get; set; } = StickUpHeightDefault;
        public double? LowestDrillElevation { get; set; } = LowestDrillElevationDefault;
    }

    // Source: DWIS.ADCSBridge.Generic.FrictionTest.ConfigurationOriginal
    public class FrictionTestConfigurationOriginal
    {
        public static double WOBDetectionThresholdDefault = 500.0;
        public double WOBDetectionThreshold { get; set; } = WOBDetectionThresholdDefault;
    }

    // Source: DWIS.ADCSBridge.Generic.Reciprocation.Configuration
    public class ReciprocationConfiguration
    {
        public static double StickUpHeightDefault = 0.8;
        public static double LowestDrillElevationDefault = 0.2;

        public bool UseSimpleMode { get; set; } = false;
        public double? StickUpHeight { get; set; } = StickUpHeightDefault;
        public double? LowestDrillElevation { get; set; } = LowestDrillElevationDefault;
    }
}
