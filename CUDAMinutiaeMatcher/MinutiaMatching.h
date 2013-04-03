#include "cuda_runtime.h"
#include "device_launch_parameters.h"
#include <math.h>
#include <stdio.h>
#include "DirectionalFiltering.h"

// GPU FUNCITONS

const int DistanceToleranceBox = 9;
const int MatchingToleranceBox = 36;
const float AngleToleranceBox = CUDART_PI_F/8;

__device__ float DetermineLength(int dx, int dy)
{
	return sqrt((float)(dx*dx+dy*dy));
}

__global__ void MatchMinutiae(CUDAArray<int> result, CUDAArray<int> X1, CUDAArray<int> X2, CUDAArray<int> Y1, CUDAArray<int> Y2)
{
	__shared__ int x1[32][32];
	__shared__ int y1[32][32];
	__shared__ int x2[32][32];
	__shared__ int y2[32][32];

	__shared__ float length1[32][32];
	__shared__ float length2[32][32];

	__shared__ float angle1[32][32];
	__shared__ float angle2[32][32];

	//each shared row corresponds to the fprint centered at its index's minutia
	int dx = X1.At(0,threadIdx.x);
	x1[threadIdx.x][threadIdx.y] = X1.At(blockIdx.x,threadIdx.y)-dx;
	dx = Y1.At(0,threadIdx.x);
	y1[threadIdx.x][threadIdx.y] = Y1.At(blockIdx.x,threadIdx.y)-dx;
	dx = X2.At(0,threadIdx.x);
	x2[threadIdx.x][threadIdx.y] = X2.At(blockIdx.x,threadIdx.y)-dx;
	dx = Y2.At(0,threadIdx.x);
	y2[threadIdx.x][threadIdx.y] = Y2.At(blockIdx.x,threadIdx.y)-dx;

	length1[threadIdx.x][threadIdx.y] = DetermineLength(x1[threadIdx.x][threadIdx.y], y1[threadIdx.x][threadIdx.y]);
	length2[threadIdx.x][threadIdx.y] = DetermineLength(x2[threadIdx.x][threadIdx.y], y2[threadIdx.x][threadIdx.y]);

	angle1[threadIdx.x][threadIdx.y] = atan2((float)-y1[threadIdx.x][threadIdx.y],(float)x1[threadIdx.x][threadIdx.y]);
	angle2[threadIdx.x][threadIdx.y] = atan2((float)-y2[threadIdx.x][threadIdx.y],(float)x2[threadIdx.x][threadIdx.y]);


	__syncthreads();
	int m =0;
	// now threadidx.x is the row for the 1st, threadidx.y - for second
	for(int i=0;i<32;i++)
	{
		if(i==threadIdx.x)continue;

		for(int j=0;j<32;j++)
		{
			if(j==threadIdx.y)continue;

			if (abs(length1[threadIdx.x][i] - length2[threadIdx.y][j]) <= DistanceToleranceBox
				&&abs(angle1[threadIdx.x][i] - angle2[threadIdx.y][j]) <= CUDART_PI_F/8) 
			{
			// do fancy stuff

				float cosine = cos(angle2[threadIdx.y][j] - angle1[threadIdx.x][i]);
				float sine = -sin(angle2[threadIdx.y][j] - angle1[threadIdx.x][i]);
				int mask = 0;
				int count=0;
				for(int m =0; m<32;m++)
				{
					float xDash = cosine * x1[threadIdx.x][m] - sine * y1[threadIdx.x][m];
				    float yDash = sine * x1[threadIdx.x][m] + cosine * y1[threadIdx.x][m];

					short nMax = -1;
					float dMax = 100500.0f;

					for(int n=0;n<32;n++)
					{
						float d = (xDash - x2[threadIdx.y][n]) * (xDash - x2[threadIdx.y][n]) + (yDash - y2[threadIdx.y][n]) * (yDash - y2[threadIdx.y][n]);
						if(d<MatchingToleranceBox&&d<dMax&&((mask>>n)&1)==0)
						{
							dMax = d;
							nMax = n;
						}
					}

					if(nMax!=-1)
					{
						mask = mask | (1<<nMax);
						count++;
					}
				}

				if(count>m)
					m=count;
			}
		}
	}

	result.SetAt(threadIdx.x,threadIdx.y,m);
}

// CPU FUNCTIONS

void MatchFingers(CUDAArray<int> x1, CUDAArray<int> y1, CUDAArray<int> x2, CUDAArray<int> y2)
{
	CUDAArray<int> result = CUDAArray<int>(32,32);

	dim3 blockSize = dim3(defaultThreadCount,defaultThreadCount);
	dim3 gridSize = 
		dim3(16,1);

	MatchMinutiae<<<gridSize, blockSize>>>(result, x1,y1,x2,y2);

	cudaError_t error;
	error = cudaDeviceSynchronize();
	int* res = result.GetData();
	int m =0;
	for(int i=0;i<1024;i++)if(res[i]>m)m=res[i];
	result.Dispose();
}