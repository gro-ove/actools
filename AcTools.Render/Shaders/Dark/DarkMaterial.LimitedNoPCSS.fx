// Alternative for DarkMaterial, only 5 extra shadows version without PCSS

#define ENABLE_SHADOWS 1
#define ENABLE_PCSS 0

#define MAX_LIGHS_AMOUNT 30
#define MAX_EXTRA_SHADOWS 5
#define MAX_EXTRA_SHADOWS_SMOOTH 1

#include "DarkMaterial.Base.fx"
#include "DarkMaterial.Reflection.fx"
#include "DarkMaterial.Lighting.Complex.fx"
#include "DarkMaterial.Kunos.fx"