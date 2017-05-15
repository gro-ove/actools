// Main, most advanced version

#define ENABLE_SHADOWS 1
#define ENABLE_PCSS 1

#define MAX_LIGHS_AMOUNT 50
#define MAX_EXTRA_SHADOWS 25

// Only for DarkKn5ObjectRenderer:
#define MAX_EXTRA_SHADOWS_SMOOTH 25
#define MAX_EXTRA_SHADOWS_FEWER 5

#include "DarkMaterial.Base.fx"
#include "DarkMaterial.Reflection.fx"
#include "DarkMaterial.Lighting.Complex.fx"
#include "DarkMaterial.Unpack.fx"
#include "DarkMaterial.Kunos.fx"