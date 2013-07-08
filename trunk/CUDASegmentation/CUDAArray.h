#include "cuda_runtime.h"
#include <stdlib.h>

#define ceilMod(x, y) (x+y-1)/y

__device__ int defaultRow()
{
	return blockIdx.y*blockDim.y+threadIdx.y;
}

__device__ int defaultColumn()
{
	return blockIdx.x*blockDim.x+threadIdx.x;
}

template<typename T>
class CUDAArray
{
private:
	size_t deviceStride;
public:
	T* cudaPtr;
	size_t Height;
	size_t Width;
	size_t Stride;

	CUDAArray()
	{
	}

	CUDAArray(const CUDAArray& arr)
	{
		cudaPtr = arr.cudaPtr;
		Height = arr.Height;
		Width = arr.Width;
		Stride = arr.Stride;
		deviceStride = arr.deviceStride;
	}

	CUDAArray(T* cpuPtr, int width, int height)
	{
		Width = width;
		Height = height;
		cudaError_t error = cudaMallocPitch((void**)&cudaPtr, &Stride, Width*sizeof(T), Height);
		error = cudaDeviceSynchronize();
		deviceStride = Stride/sizeof(T);
		error = cudaMemcpy2D(cudaPtr, Stride, cpuPtr, Width*sizeof(T), 
			Width*sizeof(T), Height, cudaMemcpyHostToDevice);
		error = cudaDeviceSynchronize();
		error = cudaGetLastError();
	}

	CUDAArray(int width, int height)
	{
		Width = width;
		Height = height;
		cudaError_t error = cudaMallocPitch((void**)&cudaPtr, &Stride, Width*sizeof(T), Height);
		error = cudaDeviceSynchronize();
		deviceStride = Stride/sizeof(T);
	}

	T* GetData()
	{
		T* arr = (T*)malloc(sizeof(T)*Width*Height);
		GetData(arr);
		return arr;
	}

	void GetData(T* arr)
	{
		cudaError_t error = cudaMemcpy2D(arr, Width*sizeof(T), cudaPtr, Stride, Width*sizeof(T), Height, cudaMemcpyDeviceToHost);
		error = cudaDeviceSynchronize();
	}

	__device__ T At(int row, int column)
	{
		return cudaPtr[row*deviceStride+column];
	}

	__device__ void SetAt(int row, int column, T value)
	{
		cudaPtr[row*deviceStride+column] = value;
	}

	void Dispose()
	{
		cudaFree(cudaPtr);
	}

	~CUDAArray()
	{
		
	}
};