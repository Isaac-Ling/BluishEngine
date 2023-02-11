﻿#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0_level_9_1
	#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

Texture2D SpriteTexture;
Texture2D Scene;
float2 SceneLocation;

sampler2D SpriteTextureSampler = sampler_state
{
	Texture = <SpriteTexture>;
	Filter = Point;
};

sampler2D SceneSampler = sampler_state
{
	Texture = <Scene>;
	Filter = Point;
};

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
	float4 Color : COLOR0;
	float2 TextureCoordinates : TEXCOORD0;
};

float4 MainPS(VertexShaderOutput input) : COLOR
{
	clip(0.5f - distance(input.TextureCoordinates, float2(0.5f, 0.5f)));
	float intensity = 0.025f / pow(distance(input.TextureCoordinates, float2(0.5f, 0.5f)), 2);
	return float4(intensity, intensity, intensity, intensity);
}

technique SpriteDrawing
{
	pass P0
	{
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};