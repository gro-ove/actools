// Alternative for DarkMaterial, without any shadows at all

#define ENABLE_SHADOWS 0
#define ENABLE_PCSS 0

#define MAX_LIGHS_AMOUNT 30
#define MAX_EXTRA_SHADOWS 0
#define MAX_EXTRA_SHADOWS_SMOOTH 0

#include "DarkMaterial.Base.fx"
#include "DarkMaterial.Reflection.fx"
#include "DarkMaterial.Lighting.Complex.fx"
#include "DarkMaterial.Kunos.fx"