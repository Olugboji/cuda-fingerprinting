
#include "cuda_runtime.h"
#include "device_launch_parameters.h"

#include <stdio.h>
#include<stdlib.h>
//#include<MinutiaMatching.h>

//cudaError_t addWithCuda(int *c, const int *a, const int *b, size_t size);
cudaError_t addWithCuda(double **picture, int size, double **result);

//__global__ void addKernel(int *c, const int *a, const int *b)
//{
//    int i = threadIdx.x;
//    c[i] = a[i] + b[i];
//}

//CUDAArray<float> loadImage(const char* name, bool sourceIsFloat = false)
//{
//	FILE* f = fopen(name,"rb");
//			
//	int width;
//	int height;
//	
//	fread(&width,sizeof(int),1,f);
//			
//	fread(&height,sizeof(int),1,f);
//	
//	float* ar2 = (float*)malloc(sizeof(float)*width*height);
//
//	if(!sourceIsFloat)
//	{
//		int* ar = (int*)malloc(sizeof(int)*width*height);
//		fread(ar,sizeof(int),width*height,f);
//		for(int i=0;i<width*height;i++)
//		{
//			ar2[i]=ar[i];
//		}
//		
//		free(ar);
//	}
//	else
//	{
//		fread(ar2,sizeof(float),width*height,f);
//	}
//	
//	fclose(f);
//
//	CUDAArray<float> sourceImage = CUDAArray<float>(ar2,width,height);
//
//	free(ar2);		
//
//	return sourceImage;
//}


__device__ double B(double *picture, int x, int y, size_t pitch)        //����� �(�) ���������� ���������� ������ �������� � ����������� ����� �
{
	return picture[x + (y - 1)*pitch] + picture[x + 1 + (y - 1)*pitch] + picture[x + 1 + y*pitch] + picture[x + 1 + (y + 1)*pitch] +
           picture[x * (y + 1)*pitch] + picture[x - 1 + (y + 1)*pitch] + picture[x - 1 + y*pitch] + picture[x - 1 * (y - 1)*pitch];
}

__device__ double A(double *picture, int x, int y, size_t pitch)        //����� �(�) ���������� ���������� ������ ������ ����� � ������ �������� ������ ����� � (..0->1..)
{
	int counter = 0;
    if((picture[x + (y - 1)*pitch] == 0) && (picture[x + 1 + (y - 1)*pitch] == 1))
    {
        counter++;
    }
    if ((picture[x + 1 + (y - 1)*pitch] == 0) && (picture[x + 1 + y*pitch] == 1))
    {
        counter++;
    }
    if ((picture[x + 1 + y*pitch] == 0) && (picture[x + 1 + (y + 1)*pitch] == 1))
    {
        counter++;
    }
    if ((picture[x + 1 + (y + 1)*pitch] == 0) && (picture[x + (y + 1)*pitch] == 1))
    {
        counter++;
    }
    if ((picture[x + (y + 1)*pitch] == 0) && (picture[x - 1 + (y + 1)*pitch] == 1))
    {
        counter++;
    }
    if ((picture[x - 1 + (y + 1)*pitch] == 0) && (picture[x - 1 + y*pitch] == 1))
    {
        counter++;
    }
    if ((picture[x - 1 + y*pitch] == 0) && (picture[x - 1 + (y - 1)*pitch] == 1))
    {
        counter++;
    }
    if ((picture[x - 1 + (y - 1)*pitch] == 0) && (picture[x + (y - 1)*pitch] == 1))
    {
        counter++;
    }
    return counter;
}


__global__ void ThiningPictureWithCUDA(double* newPicture,double *thinnedPicture ,size_t pitch, int width, int height)
{
	double *picture = newPicture;
	int x = threadIdx.x + blockIdx.x*blockDim.x;
    int y = threadIdx.y + blockIdx.y*blockDim.y;
    if((x > 0) && (y > 0) && (x < width) && (y < height))
	{             
		if ((picture[x, y] == 1) && (2 <= B(picture, x, y, pitch)) && (B(picture, x, y, pitch) <= 6) && (A(picture, x, y, pitch) == 1) &&     //���������������� �������� �����, ��. Zhang-Suen thinning algorithm, http://www-prima.inrialpes.fr/perso/Tran/Draft/gateway.cfm.pdf
            (picture[x + (y - 1)*pitch]*picture[x + 1 + y*pitch]*picture[x + (y + 1)*pitch] == 0) &&
            (picture[x + 1 + y*pitch]*picture[x + (y + 1)*pitch]*picture[x - 1 + y*pitch] == 0))
        {
            picture[x + y*pitch] = 0;
        }
		
		if ((picture[x + y*pitch] == 1) && (2 <= B(picture, x, y, pitch)) && (B(picture, x, y, pitch) <= 6) && (A(picture, x, y, pitch) == 1) &&
			(picture[x + (y - 1)*pitch] * picture[x + 1 + y*pitch] * picture[x - 1 + y*pitch] == 0) &&
			(picture[x * (y - 1)*pitch] * picture[x + (y + 1)*pitch] * picture[x - 1 + y*pitch] == 0))
		{
			picture[x + y*pitch] = 0;
		} 
		
		if ((picture[x, y] == 1) &&
            (((picture[x, (y - 1)*pitch] * picture[x + 1 + y*pitch] == 1) && (picture[x - 1 + (y + 1)*pitch] != 1)) || ((picture[x + 1 + y*pitch] * picture[x + (y + 1)*pitch] == 1) && (picture[x - 1 + (y - 1)*pitch] != 1)) ||      //��������� ����������� ��������� ��� ��� �������� ����������
            ((picture[x + (y + 1)*pitch] * picture[x - 1 + y*pitch] == 1) && (picture[x + 1 + (y - 1)*pitch] != 1)) || ((picture[x + (y - 1)*pitch] * picture[x - 1 + y*pitch] == 1) && (picture[x + 1 + (y + 1)*pitch] != 1))))
        {
            picture[x + y*pitch] = 0;
        }
		
		thinnedPicture = picture;
	}
}








int main()
{
//    const int arraySize = 5;
//    const int a[arraySize] = { 1, 2, 3, 4, 5 };
//    const int b[arraySize] = { 10, 20, 30, 40, 50 };
//    int c[arraySize] = { 0 };

    // Add vectors in parallel.
	int size = 32;
	double **picture = (double**)malloc(size*size*sizeof(double*));
	for(int i = 0; i < size; i++){
		picture[i] = (double*)malloc(size*sizeof(double));
	}
	double **result = (double**)malloc(size*size*sizeof(double*));
	for(int i = 0; i < size; i++){
		result[i] = (double*)malloc(size*sizeof(double));
	}
	for(int i = 0; i < size; i++)
	{
		for(int j = 0; j < size; j++)
		{
			scanf("%d",&picture[i][j]);
		}
	}

    cudaError_t cudaStatus = addWithCuda(picture, size, result);
    if (cudaStatus != cudaSuccess) {
        fprintf(stderr, "addWithCuda failed!");
        return 1;
    }

    // cudaDeviceReset must be called before exiting in order for profiling and
    // tracing tools such as Nsight and Visual Profiler to show complete traces.
//    cudaStatus = cudaDeviceReset();
//    if (cudaStatus != cudaSuccess) {
//        fprintf(stderr, "cudaDeviceReset failed!");
//        return 1;
//    }
	for(int i = 0; i < size; i++){
		free(picture[i]);
	}
	free(picture);\
	for(int i = 0; i < size; i++){
		free(result[i]);
	}
	free(result);

    return 0;
}

// Helper function for using CUDA to add vectors in parallel.
cudaError_t addWithCuda(double **picture, int size, double **result)
{
    //int *dev_a = 0;
    //int *dev_b = 0;
    //int *dev_c = 0;
	double* dev_picture;
	double* dev_pictureThinned;
	int width, height;
	width = size;
	height = size;

    cudaError_t cudaStatus;
	size_t pitch;
    // Choose which GPU to run on, change this on a multi-GPU system.
    cudaStatus = cudaSetDevice(0);
    if (cudaStatus != cudaSuccess) {
        fprintf(stderr, "cudaSetDevice failed!  Do you have a CUDA-capable GPU installed?");
        goto Error;
    }
	//Allocate GPU buffers for picture.
	cudaError_t cudastatus;
	cudaStatus = cudaMallocPitch((void**)&dev_picture, &pitch, width*sizeof(int), height);
	if (cudaStatus != cudaSuccess) {
        fprintf(stderr, "cudaMallocPitch!");
        goto Error;
    }
    //cudaStatus = cudaMalloc((void**)&dev_a, size * sizeof(int));
    //if (cudaStatus != cudaSuccess) {
    //    fprintf(stderr, "cudaMalloc failed!");
    //    goto Error;
    //}

    //cudaStatus = cudaMalloc((void**)&dev_b, size * sizeof(int));
    //if (cudaStatus != cudaSuccess) {
    //    fprintf(stderr, "cudaMalloc failed!");
    //    goto Error;
    //}

    // Copy input vpicture from host memory to GPU buffers.
    cudaStatus = cudaMemcpy2D(dev_picture, pitch, picture, width*sizeof(int), width*sizeof(int), height, cudaMemcpyHostToDevice);
    if (cudaStatus != cudaSuccess) {
        fprintf(stderr, "cudaMalloc failed!");
        goto Error;
    }

    //cudaStatus = cudaMemcpy(dev_a, a, size * sizeof(int), cudaMemcpyHostToDevice);
    //if (cudaStatus != cudaSuccess) {
    //    fprintf(stderr, "cudaMemcpy failed!");
    //    goto Error;
    //}

    //cudaStatus = cudaMemcpy(dev_b, b, size * sizeof(int), cudaMemcpyHostToDevice);
    //if (cudaStatus != cudaSuccess) {
    //    fprintf(stderr, "cudaMemcpy failed!");
    //    goto Error;
    //}

    // Launch a kernel on the GPU with one thread for each element.
    ThiningPictureWithCUDA<<<1, size>>>(dev_picture, dev_pictureThinned, pitch, width, height);

    // cudaDeviceSynchronize waits for the kernel to finish, and returns
    // any errors encountered during the launch.
    cudaStatus = cudaDeviceSynchronize();
    if (cudaStatus != cudaSuccess) {
        fprintf(stderr, "cudaDeviceSynchronize returned error code %d after launching addKernel!\n", cudaStatus);
        goto Error;
    }

    // Copy output vector from GPU buffer to host memory.
	cudastatus = cudaMemcpy2D(result,width*sizeof(int),dev_pictureThinned,pitch,width*sizeof(int),height,cudaMemcpyDeviceToHost);
    if (cudaStatus != cudaSuccess) {
        fprintf(stderr, "cudaMemcpy failed!");
        goto Error;
    }

Error:
    cudaFree(dev_picture);
    cudaFree(dev_pictureThinned);
    //cudaFree(dev_b);
    
    return cudaStatus;
}
