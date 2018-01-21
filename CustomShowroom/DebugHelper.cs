namespace CustomShowroom {
    public class DebugHelper {
        public static string GetCarKn5() {
            var kn5file = @"D:\Games\Assetto Corsa\content\cars\ks_alfa_romeo_gta\alfaromeo_gta.kn5";
            //kn5file = @"D:\Games\Steam\SteamApps\common\assettocorsa\content\cars\bmw_m3_e92\bmw_m3_e92.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ferrari_f2002\ferrari_f2002.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\F1ASR_ferrari_643\F1ASR_ferrari_643.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ks_lamborghini_gallardo_sl\lamborghini_gallardo_sl.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ks_lamborghini_huracan_st\lamborghini_huracan_st.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ks_alfa_mito_qv\alfa_romeo_mito_qv.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ks_abarth_595ss\abarth_595ss.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ks_abarth_595ss_s2\abarth_595ss.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ks_alfa_romeo_155_v6\Alfa_Romeo_155_V6.kn5";
            //kn5file = @"D:\Games\Steam\SteamApps\common\assettocorsa\content\cars\ferrari_250gt_lusso\ferrari_250gt_lusso.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ks_audi_r8_plus\audi_r8_plus.kn5";
            //kn5file = @"D:\Games\Steam\SteamApps\common\assettocorsa\content\cars\lotus_2_eleven\lotus_2_eleven.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ruf_yellowbird\ruf_yellowbird.kn5";
            //kn5file = @"D:\Games\Steam\SteamApps\common\assettocorsa\content\cars\abarth500\abarth500.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ks_praga_r1\praga_r1.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\mercedes_sls\mercedes_sls.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\pagani_huayra\pagani_huayra.kn5";

            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ks_ruf_rt12r\ruf_rt12r.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\reliant_robin\robin1.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ks_alfa_romeo_155_v6\collider.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ks_alfa_romeo_155_v6\Alfa_Romeo_155_V6.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ks_alfa_romeo_4c\alfa_romeo_4c.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ariel_atom_v8\ariel_atom_v8.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\peugeot_504\peugeot_504.kn5";
            //kn5file = @"D:\Games\Steam\SteamApps\common\assettocorsa\content\cars\mtx_103c\mtx_103c.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ferrari_f40\ferrari_f40.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ferrari_f40_s3\ferrari_f40.kn5";
            //kn5file = @"D:\Games\Steam\SteamApps\common\assettocorsa\content\cars\ks_ferrari_488_gt3\ferrari_488_gt3_lod_a.kn5";
            //kn5file = @"D:\Games\Steam\SteamApps\common\assettocorsa\content\cars\ks_lotus_72d\lotus_72d.kn5";

            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ks_mercedes_c9\mercedes_c9.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ks_nissan_gtr\nissan_gtr.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\mercedes_sls\mercedes_sls.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\acc_orion_mk7_ng\acc_orion_mk7_ng.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ks_mclaren_p1\mclaren_p1.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ks_porsche_917_30\porsche_917_30.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\acc_nissan_r33_drift\acc_nissan_r33_drift.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ktm_xbow_r\ktm_xbow_r.kn5";
            //kn5file = @"D:\Games\Steam\SteamApps\common\assettocorsa\content\cars\ks_porsche_911_gt1\porsche_911_gt1.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ks_lamborghini_countach\lamborghini_countach.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ks_lamborghini_miura_sv\lamborghini_miura_sv.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ks_lotus_25\lotus_25.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ks_lotus_72d\lotus_72d.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\lotus_49\lotus_49.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\peugeot_504_tn\peugeot_504.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\acc_porsche_914-6\porsche_914-6.kn5";
            //kn5file = @"D:\Games\Steam\SteamApps\common\assettocorsa\content\cars\vaz2106\vaz2106.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ktm_xbow_r\ktm_xbow_r.kn5"; // WARN
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ks_bmw_m4\bmw_m4.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\lotus_elise_sc\lotus_elise_sc.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\gmc_vandura\gmc_vandura.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ks_corvette_c7r\corvette_c7r.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ks_ford_escort_mk1\ford_escort_mk1.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ks_ford_mustang_2015\ford_mustang_2015.kn5";
            //kn5file = @"U:\ShowroomPreviewSphere\sphere.kn5";
            //kn5file = @"D:\Games\Steam\SteamApps\common\assettocorsa\content\cars\jaguar_etype\jaguar_etype.kn5";
            //kn5file = @"D:\Games\Steam\SteamApps\common\assettocorsa\content\cars\estonia_21\estonia_21.kn5";

            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ks_alfa_romeo_155_v6\Alfa_Romeo_155_V6_LOD_B.kn5";
            //kn5file = @"D:\Games\Steam\SteamApps\common\assettocorsa\content\cars\bmw_m3_e30_dtm\bmw_m3_e30_grC.kn5";
            //kn5file = @"D:\Games\Steam\SteamApps\common\assettocorsa\content\cars\fiat_131_abarth\fiat_131_abarth.kn5";
            //kn5file = @"D:\Games\Steam\SteamApps\common\assettocorsa\content\cars\alfa_romeo_33\alfa_romeo_33.kn5";
            //kn5file = @"D:\Games\Steam\SteamApps\common\assettocorsa\content\cars\bmw_m3_e30\bmw_m3_e30.kn5";
            //kn5file = @"D:\Games\Steam\SteamApps\common\assettocorsa\content\cars\ft_morgan_3_wheeler\ft_morgan_3_wheeler.kn5";
            //kn5file = @"D:\Games\Steam\SteamApps\common\assettocorsa\content\cars\ks_ruf_rt12r\ruf_rt12r.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\acc_orion_mk7_ng\acc_orion_mk7_ng.kn5";
            //kn5file = @"D:\Games\Steam\SteamApps\common\assettocorsa\content\cars\__balls_refl\sphere.kn5";
            //kn5file = @"D:\Games\Steam\SteamApps\common\assettocorsa\content\cars~\__sphere\sphere.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\tracks\topgear\topgear.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ks_toyota_supra_mkiv_tuned\toyota_supra_drift2.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\ks_abarth500_assetto_corse\abarth500_assetto_corse.kn5";
            //kn5file = @"D:\Games\Assetto Corsa\content\cars\alfa_romeo_giulietta_qv\alfa_romeo_giulietta_QV.kn5";
            return kn5file;
        }

        public static string GetShowroomKn5() {
            var showroomKn5File = @"D:\Games\Assetto Corsa\content\showroom\showroom\showroom.kn5";
            //showroomKn5File = @"D:\Games\Assetto Corsa\content\showroom\industrial\industrial.kn5";
            //showroomKn5File = @"D:\Games\Assetto Corsa\content\showroom\hdri_2\hdri_2.kn5";
            //showroomKn5File = @"D:\Games\Assetto Corsa\content\showroom\studio_smoke\studio_smoke.kn5";
            //showroomKn5File = @"D:\Games\Assetto Corsa\content\showroom\at_previews\at_previews.kn5";
            //showroomKn5File = @"D:\Games\Steam\SteamApps\common\assettocorsa\content\showroom\gasstation\gasstation.kn5";
            //showroomKn5File = @"D:\Games\Assetto Corsa\content\showroom\big_shop\big_shop.kn5";
            //showroomKn5File = @"D:\Games\Assetto Corsa\content\showroom\sr_hl\sr_hl.kn5";
            //showroomKn5File = @"D:\Games\Assetto Corsa\content\showroom\greenfield\greenfield.kn5";
            //showroomKn5File = @"D:\Games\Steam\SteamApps\common\assettocorsa\content\showroom\underpass\underpass.kn5";
            //showroomKn5File = @"D:\Games\Steam\SteamApps\common\assettocorsa\content\showroom\needfor_uc\needfor_uc.kn5";
            //showroomKn5File = null;
            return showroomKn5File;
        }
    }
}