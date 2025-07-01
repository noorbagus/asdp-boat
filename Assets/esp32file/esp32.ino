#include <BLEDevice.h>
#include <BLEServer.h>
#include <BLEUtils.h>
#include <BLE2902.h>
#include <Wire.h>
#include <MPU6050.h>

// ESP32BLE Plugin UUIDs
#define SERVICE_UUID        "4fafc201-1fb5-459e-8fcc-c5c9c331914b"
#define CHARACTERISTIC_UUID "beb5483e-36e1-4688-b7f5-ea07361b26a8"

BLEServer* pServer = NULL;
BLECharacteristic* pCharacteristic = NULL;
bool deviceConnected = false;

MPU6050 mpu;
#define LED_PIN 2

float phase = 0;
unsigned long lastSend = 0;

class MyServerCallbacks: public BLEServerCallbacks {
    void onConnect(BLEServer* pServer) {
      deviceConnected = true;
      Serial.println("*** CLIENT CONNECTED ***");
      digitalWrite(LED_PIN, HIGH);
    };

    void onDisconnect(BLEServer* pServer) {
      deviceConnected = false;
      Serial.println("*** CLIENT DISCONNECTED ***");
      digitalWrite(LED_PIN, LOW);
      
      // Restart advertising
      delay(500);
      pServer->getAdvertising()->start();
      Serial.println("Restarting advertising...");
    }
};

void setup() {
  Serial.begin(115200);
  pinMode(LED_PIN, OUTPUT);
  
  Wire.begin();
  mpu.initialize();
  
  Serial.println("Starting BLE work!");

  // Initialize BLE
  BLEDevice::init("ferizy-paddle");
  pServer = BLEDevice::createServer();
  pServer->setCallbacks(new MyServerCallbacks());

  // Create service
  BLEService *pService = pServer->createService(SERVICE_UUID);

  // Create characteristic
  pCharacteristic = pService->createCharacteristic(
                      CHARACTERISTIC_UUID,
                      BLECharacteristic::PROPERTY_READ |
                      BLECharacteristic::PROPERTY_WRITE |
                      BLECharacteristic::PROPERTY_NOTIFY
                    );

  pCharacteristic->addDescriptor(new BLE2902());

  // Start service
  pService->start();

  // Start advertising
  BLEAdvertising *pAdvertising = BLEDevice::getAdvertising();
  pAdvertising->addServiceUUID(SERVICE_UUID);
  pAdvertising->setScanResponse(false);
  pAdvertising->setMinPreferred(0x0);
  BLEDevice::startAdvertising();
  
  Serial.println("Characteristic defined! Now you can connect with your phone!");
  
  // LED startup signal
  for(int i = 0; i < 5; i++) {
    digitalWrite(LED_PIN, HIGH);
    delay(100);
    digitalWrite(LED_PIN, LOW);
    delay(100);
  }
}

void loop() {
  if (deviceConnected) {
    // Generate dummy oscillating data
    phase += 0.05;
    int16_t ax = (int16_t)(16000 * sin(phase));
    
    // Calculate roll angle
    float roll = atan2(ax, 16384) * 180.0 / PI;
    
    // Send data every 50ms
    if (millis() - lastSend > 50) {
      String data = "A:" + String(roll, 1);
      pCharacteristic->setValue(data.c_str());
      pCharacteristic->notify();
      
      Serial.println("Sent: " + data);
      lastSend = millis();
      
      // Quick LED blink
      digitalWrite(LED_PIN, LOW);
      delay(5);
      digitalWrite(LED_PIN, HIGH);
    }
  } else {
    // Slow blink when not connected
    static unsigned long lastBlink = 0;
    if (millis() - lastBlink > 1000) {
      digitalWrite(LED_PIN, !digitalRead(LED_PIN));
      lastBlink = millis();
      Serial.println("Waiting for connection...");
    }
  }
  
  delay(20);
}