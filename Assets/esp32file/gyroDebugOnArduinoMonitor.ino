#include "BluetoothSerial.h"
#include <Wire.h>
#include <MPU6050.h>

BluetoothSerial SerialBT;
MPU6050 mpu;

#define LED_PIN 2

// Timing
unsigned long lastSendTime = 0;
const unsigned long sendInterval = 50; // 20Hz

// Thermal protection
float maxSafeTemp = 70.0;
bool thermalShutdown = false;

void setup() {
  Serial.begin(115200);
  pinMode(LED_PIN, OUTPUT);
  
  // LED startup
  for(int i = 0; i < 3; i++) {
    digitalWrite(LED_PIN, HIGH);
    delay(200);
    digitalWrite(LED_PIN, LOW);
    delay(200);
  }
  
  // Initialize I2C
  Wire.begin();
  
  // Initialize MPU6050
  Serial.println("Initializing MPU6050...");
  mpu.initialize();
  
  if (mpu.testConnection()) {
    Serial.println("MPU6050 OK");
    digitalWrite(LED_PIN, HIGH);
    delay(500);
    digitalWrite(LED_PIN, LOW);
  } else {
    Serial.println("MPU6050 FAILED");
    while(1) {
      digitalWrite(LED_PIN, HIGH);
      delay(100);
      digitalWrite(LED_PIN, LOW);
      delay(100);
    }
  }
  
  // Configure MPU6050
  mpu.setFullScaleGyroRange(MPU6050_GYRO_FS_500);      // ±500°/s
  mpu.setFullScaleAccelRange(MPU6050_ACCEL_FS_4);      // ±4g
  mpu.setDLPFMode(MPU6050_DLPF_BW_20);                 // 20Hz filter
  
  // Initialize Bluetooth
  Serial.println("Starting Bluetooth...");
  
  // Use simple setup for Windows compatibility
  if (!SerialBT.begin("ferizy")) {
    Serial.println("Bluetooth FAILED");
    while(1) {
      digitalWrite(LED_PIN, HIGH);
      delay(50);
      digitalWrite(LED_PIN, LOW);
      delay(50);
    }
  }
  
  // Register connection callbacks
  SerialBT.register_callback([](esp_spp_cb_event_t event, esp_spp_cb_param_t *param) {
    if (event == ESP_SPP_SRV_OPEN_EVT) {
      Serial.println("✓ Windows client connected!");
    } else if (event == ESP_SPP_CLOSE_EVT) {
      Serial.println("✗ Windows client disconnected");
    }
  });
  
  Serial.println("ESP32 Ready for Windows 11");
  Serial.println("Device: ferizy");
  Serial.println("No PIN required - auto-pairing enabled");
}

void loop() {
  // Check connection status
  bool connected = SerialBT.hasClient();
  
  if (connected) {
    // Connected - steady LED
    digitalWrite(LED_PIN, HIGH);
  } else {
    // Not connected - slow blink + status log
    static unsigned long lastBlink = 0;
    static unsigned long lastStatusLog = 0;
    
    if (millis() - lastBlink > 1000) {
      digitalWrite(LED_PIN, !digitalRead(LED_PIN));
      lastBlink = millis();
    }
    
    // Log waiting status every 2 seconds
    if (millis() - lastStatusLog > 2000) {
      Serial.println("Waiting for Bluetooth connection...");
      lastStatusLog = millis();
    }
  }
  
  // Send sensor data regardless of connection status
  if (millis() - lastSendTime >= sendInterval) {
    sendSensorData();
    lastSendTime = millis();
  }
  
  delay(10);
}

void sendSensorData() {
  // Get raw sensor data
  int16_t ax, ay, az;
  int16_t gx, gy, gz;
  
  mpu.getMotion6(&ax, &ay, &az, &gx, &gy, &gz);
  
  // Convert gyro to degrees/second
  float gyroX = gx / 65.5;  // For ±500°/s range
  float gyroY = gy / 65.5;
  float gyroZ = gz / 65.5;
  
  // Get temperature
  float temp = mpu.getTemperature() / 340.0 + 36.53;
  
  // Print to Serial Monitor for testing
  Serial.print("Gyro X: "); Serial.print(gyroX, 1);
  Serial.print(" | Y: "); Serial.print(gyroY, 1);
  Serial.print(" | Z: "); Serial.print(gyroZ, 1);
  Serial.print(" | Accel X: "); Serial.print(ax);
  Serial.print(" | Y: "); Serial.print(ay);
  Serial.print(" | Z: "); Serial.print(az);
  Serial.print(" | Temp: "); Serial.println(temp, 1);
  
  // Also send via Bluetooth if connected
  if (SerialBT.hasClient()) {
    String dataString = "G:";
    dataString += String(gyroX, 1);
    dataString += ",";
    dataString += String(gyroY, 1);
    dataString += ",";
    dataString += String(gyroZ, 1);
    dataString += ",A:";
    dataString += String(ax);
    dataString += ",";
    dataString += String(ay);
    dataString += ",";
    dataString += String(az);
    dataString += ",T:";
    dataString += String(temp, 1);
    
    SerialBT.println(dataString);
  }
  
  // Quick LED blink for data transmission
  digitalWrite(LED_PIN, LOW);
  delay(5);
  digitalWrite(LED_PIN, HIGH);
}