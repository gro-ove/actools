// Main, most advanced version

#define ENABLE_SHADOWS 1
#define ENABLE_PCSS 1

#define MAX_LIGHS_AMOUNT 30
#define MAX_EXTRA_SHADOWS 15

// Only for DarkKn5ObjectRenderer:
#define MAX_EXTRA_SHADOWS_SMOOTH 15
#define MAX_EXTRA_SHADOWS_LIMITED 5

// For faster compilation, limit extra shadows to one
#define COMPLEX_LIGHTING_DEBUG_MODE 1

#include "DarkMaterial.Base.fx"
#include "DarkMaterial.Reflection.2.fx"
#include "DarkMaterial.Lighting.Complex.2.fx"
#include "DarkMaterial.Unpack.fx"
#include "DarkMaterial.Kunos.5_0.fx"