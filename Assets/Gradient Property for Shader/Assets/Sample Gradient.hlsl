#ifndef GRADIENTS_INCLUDED
#define GRADIENTS_INCLUDED

// Samples a vertical gradient texture using a scalar t
float4 SampleGradient(sampler2D gradient, float2 uv)
{
    return tex2D(gradient, uv);
}

#endif