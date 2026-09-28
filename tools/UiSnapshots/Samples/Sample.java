//DEPS com.google.code.gson:gson:2.11.0
import com.google.gson.Gson;

public class Sample {
    public static void main(String[] args) {
        Gson gson = new Gson();
        System.out.println("Java Maven Demo: " + gson.toJson("Hello World"));
    }
}
