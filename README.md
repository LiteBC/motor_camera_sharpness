### Test Assignment: Motor and Camera Integration

**Objective:**  
Implement a solution that integrates a motor controller and a camera. The goal is to move the 
motor while capturing images, analyze the captured images to determine the sharpest one, and 
return the motor to the position where the sharpest image was recorded. 
That is, when you move the motor, the camera image will change, and you have to select the point 
where the image looks the sharpest (in focus). You can expect this point to be within the motor 
movement range. 

Use the Program.cs file as the entry point for your application - it already has some hints
and examples. Change it to your needs.

**General conditions:**
- You are free to take this assignment any time you prefer.
- You should write it in C#, unless you have a strong reason to also use another language.
- We suggest using Visual Studio 2022, but you can use any IDE you prefer.
- You can use any tools available, any help, whatever libraries you want. We expect you to own the resulting code - that is, to be able to explain it and to continue development in the real lab setup.
- You are welcome to save the images from the camera – to inspect them visually, get a grasp on the data you’re dealing with. 



**Time limit:** 
This task should not take more than 6 hours.
We'll appreciate if you send us the solution as soon as you're done with it - to let us evaluate it comfortably.

There is no time limit on camera movement and processing time – but you are welcome to analyze and try to 
speed up the software, while maintaining the valid result output.

** Follow-up:**
This homework assignment will be followed by a session in the lab with a real device setup. 
You can expect them to implement the same interfaces: IFrameProducer and IMotorController.

---

**Requirements:**

0. **Initialize (fake) Hardware:**
   - Use the provided `FakeCamera` and `FakeMotorController` classes to simulate hardware.

1. **Motor Movement:**
   - Use the `IMotorController` interface to control the motor.
   - Move the motor across a predefined range of positions - see examples in Program.cs.

2. **Image Capture:**
   - Use the `IFrameProducer` interface to capture images at each motor position.

3. **Sharpness Analysis:**
   - Implement a method to evaluate the sharpness of each captured image.
   	- suggestion: ATEN (sometimes also called Absolute Total Edge Norm).
   - Plot image sharpness vs. coordinate. 
   - Select the sharpest image based on the evaluation.


4. **Return to Position:**
   - Record the motor position where the sharpest image was captured.
   - Move the motor back to this position after the analysis is complete.

5. **Code Quality:**
   - Write clean, readable, and maintainable code.
   - Use appropriate design patterns and principles.

6. **Testing:**
   - Use the `FakeCamera` and `FakeMotorController` implementations to simulate the camera and motor behavior.

---

**Deliverables:**
- Functionality implemented within the provided project.
- Sharpness plot (your implemented sharpness metric vs. coordinate).
- A brief (3 to 4 sentence) explanation of your solution design.

---


# Project File Descriptions

### `FakeCamera.cs`
This file contains a simulated implementation of a camera, fixed on a motor-controlled stage.
. Use it to get the images 
for the home assignment. It implements `IFrameProducer`.

### `FakeMotorController.cs`
This file provides a fake or simulated implementation of a motor controller. 
It might be used to test motor-related functionality without requiring actual hardware. 
Implements `IMotorController`.

### `IFrameProducer.cs`
This file defines an interface for frame-producing components, such as cameras or other 
image sources. It might include methods for capturing or retrieving frames.

### `IMotorController.cs`
This file defines an interface for motor controllers. 
It might include methods for controlling motor operations, such as starting, stopping, 
or adjusting speed and direction.

### `Program.cs`
This is the entry point of the application. 
It contains the `Main` method, which initializes and runs the application. 
It might set up dependencies, configure services, and start the main logic of the program.

