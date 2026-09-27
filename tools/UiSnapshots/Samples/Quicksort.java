import java.util.Arrays;

/**
 * Quicksort implementation in Java.
 */
public class Quicksort {
    public static void main(String[] args) {
        int[] data = { 64, 34, 25, 12, 22, 11, 90, 88, 45, 50, 7 };
        System.out.println("Original array: " + Arrays.toString(data));

        quicksort(data, 0, data.length - 1);

        System.out.println("Sorted array:   " + Arrays.toString(data));
        System.out.println("Minimum: " + data[0] + ", Maximum: " + data[data.length - 1]);
        System.out.println("Total elements sorted: " + data.length);
    }

    private static void quicksort(int[] arr, int low, int high) {
        if (low < high) {
            int pi = partition(arr, low, high);
            quicksort(arr, low, pi - 1);
            quicksort(arr, pi + 1, high);
        }
    }

    private static int partition(int[] arr, int low, int high) {
        int pivot = arr[high];
        int i = low - 1;
        for (int j = low; j < high; j++) {
            if (arr[j] <= pivot) {
                i++;
                int temp = arr[i];
                arr[i] = arr[j];
                arr[j] = temp;
            }
        }
        int temp = arr[i + 1];
        arr[i + 1] = arr[high];
        arr[high] = temp;
        return i + 1;
    }
}
