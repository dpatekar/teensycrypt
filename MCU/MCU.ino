#include <aes.h>
#define blockSize 4096

enum workMode {
  enc,
  dec
};
byte key[] = 
{
  0x64, 0x34, 0x90, 0x67, 0x01, 0x31, 0x20, 0x11, 0x80, 0x67, 0x03, 0x43, 0x99, 0x77, 0x22, 0x34,
  0x86, 0x61, 0x18, 0x23, 0x07, 0x04, 0x76, 0x18, 0x69, 0x08, 0x99, 0xF7, 0xA4, 0xC4, 0xD4, 0xEE
} 
;
byte encsw[] =
{
  0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
} 
;
byte decsw[] =
{
  0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
} 
;

byte inBuffer[blockSize], tempiBuffer[16], outBuffer[16], lenBuffer[4], iv[16] ;
uint len;
workMode mode;

aes_context ac;

void setup() {
  Serial.begin(115200);
  //mijenja ponasanje Serial.readBytes - bez cekanja
  Serial.setTimeout(0);
  aes_set_key(key, 32, &ac);
  pinMode(13, OUTPUT);
}

void loop() {      
  //ceka do pojave 32 byte i provjerava da li se radi o switch-u
  while (Serial.available() < 32){};  
  Serial.readBytes((char *)inBuffer, 32);
  if (memcmp(inBuffer, encsw, 32) == 0){    
    mode = enc;      
  }
  else if (memcmp(inBuffer, decsw, 32) == 0){
    mode = dec; 
  }
  else{
    //primljen je podatak koji nije switch pa se prazni serial buffer i prekida s izvršavanjem trenutnog loop-a
    Serial.flush();
    return; 
  }
  
  digitalWrite(13, HIGH);
  
  //citanje duljine dolazne datoteke
  while (Serial.available() < 4){};  
  Serial.readBytes((char *)lenBuffer, 4);
  len = *(uint*)lenBuffer; 
  
  int incomingBlocks = (len % 4096 != 0) ? ((len / 4096) + 1) : (len / 4096);
  
  if (mode == enc){  
    //generiranje IV-a
    for(size_t i = 0; i < 16; i++)
      iv[i] = rand() % 256;

    Serial.write(lenBuffer, 4);
    Serial.write(iv, 16);            
  }
  else if (mode == dec){
    //citanje IV-a
    while (Serial.available() < 16){};  
    Serial.readBytes((char *)iv, 16);
  }

  int totalReceived, iterReceived, receivedBlocks = 0;

  while (receivedBlocks < incomingBlocks){
    totalReceived = 0;
    while (totalReceived < blockSize){
      iterReceived = Serial.readBytes((char *)inBuffer + totalReceived, blockSize - totalReceived);
      if (iterReceived == 0){
        while (!Serial.available()) ;
      }   
      totalReceived += iterReceived;
    }

    receivedBlocks++;

    for (int i = 0; i < blockSize; i = i + 16){
      memcpy(tempiBuffer, inBuffer + i, 16);
      switch (mode){
      case enc:                 
        aes_cbc_encrypt(tempiBuffer, outBuffer, 1, iv, &ac);
        break;
      case dec:            
        aes_cbc_decrypt(tempiBuffer, outBuffer, 1, iv, &ac);
        break;
      }
      Serial.write(outBuffer, 16); 
    }
  }
  
  digitalWrite(13, LOW);
}
