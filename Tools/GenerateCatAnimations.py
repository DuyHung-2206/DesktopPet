"""
GenerateCatAnimations.py - High-fidelity pixel-art generator for missing Cat animation sprite sheets.
Uses existing Cat sprite frames as reference to preserve exact character proportions, palette, and styling.
"""
import os
import math
from PIL import Image, ImageDraw

CELL_SIZE = 64
BASELINE_Y = 57

CAT_DIR = "Assets/Pets/Cat"

# Colors from Cat palette
COLOR_OUTLINE = (50, 30, 20, 255)
COLOR_FUR_MAIN = (252, 147, 37, 255)
COLOR_FUR_SHADOW = (230, 81, 0, 255)
COLOR_CREAM = (255, 224, 130, 255)
COLOR_PINK = (255, 138, 128, 255)
COLOR_NOSE = (216, 27, 96, 255)
COLOR_EYE = (33, 33, 33, 255)
COLOR_WHITE = (255, 255, 255, 255)
COLOR_DIRT = (121, 85, 72, 230)
COLOR_DIRT_DARK = (93, 64, 55, 255)
COLOR_WATER = (79, 195, 247, 220)
COLOR_WATER_LIGHT = (179, 229, 252, 240)
COLOR_ANGER = (229, 57, 53, 255)
COLOR_SICK_ICE = (128, 222, 234, 255)
COLOR_BALL = (233, 30, 99, 255)
COLOR_BALL_YELLOW = (255, 235, 59, 255)

def get_base_frames():
    idle_im = Image.open(os.path.join(CAT_DIR, "Idle.png")).convert("RGBA")
    walk_im = Image.open(os.path.join(CAT_DIR, "Walk.png")).convert("RGBA")
    sleep_im = Image.open(os.path.join(CAT_DIR, "Sleep.png")).convert("RGBA")
    happy_im = Image.open(os.path.join(CAT_DIR, "Happy.png")).convert("RGBA")
    hurt_im = Image.open(os.path.join(CAT_DIR, "Hurt.png")).convert("RGBA")
    
    def get_frame(img, idx, size=64):
        return img.crop((idx * size, 0, (idx + 1) * size, size))

    return {
        "idle": get_frame(idle_im, 0),
        "walk0": get_frame(walk_im, 0),
        "walk1": get_frame(walk_im, 1),
        "walk2": get_frame(walk_im, 2),
        "walk3": get_frame(walk_im, 3),
        "sleep0": get_frame(sleep_im, 0),
        "sleep1": get_frame(sleep_im, 1),
        "sleep2": get_frame(sleep_im, 2),
        "happy0": get_frame(happy_im, 0),
        "happy1": get_frame(happy_im, 1),
        "happy2": get_frame(happy_im, 2),
        "happy3": get_frame(happy_im, 3),
        "hurt0": get_frame(hurt_im, 0),
        "hurt1": get_frame(hurt_im, 1)
    }

def shift_pixels(src_img, dx, dy):
    res = Image.new("RGBA", (CELL_SIZE, CELL_SIZE), (0, 0, 0, 0))
    res.paste(src_img, (dx, dy), src_img)
    return res

def save_strip(frames, filename):
    count = len(frames)
    strip = Image.new("RGBA", (count * CELL_SIZE, CELL_SIZE), (0, 0, 0, 0))
    for i, frame in enumerate(frames):
        strip.paste(frame, (i * CELL_SIZE, 0), frame)
    out_path = os.path.join(CAT_DIR, filename)
    strip.save(out_path, "PNG")
    print(f"Saved {out_path} ({count * CELL_SIZE}x{CELL_SIZE}, {count} frames)")
    
    # Also mirror to output build directories if they exist
    for sub in ["bin/Debug/net8.0-windows/Assets/Pets/Cat", "Publish/DesktopPetWorld/Assets/Pets/Cat", "Build/Release/win-x64/Assets/Pets/Cat"]:
        if os.path.exists(sub):
            dest = os.path.join(sub, filename)
            strip.save(dest, "PNG")

def generate_all():
    base = get_base_frames()
    idle = base["idle"]
    sleep0 = base["sleep0"]
    sleep1 = base["sleep1"]
    happy0 = base["happy0"]
    happy1 = base["happy1"]
    walk0 = base["walk0"]
    walk2 = base["walk2"]
    hurt0 = base["hurt0"]

    # 1. Idle (4 frames: smooth breathing + subtle tail flick)
    # Frame 0: Normal standing
    # Frame 1: Chest rise 1px
    # Frame 2: Peak breath + tail flick
    # Frame 3: Breathe down
    f0 = idle.copy()
    f1 = idle.copy()
    # lift chest slightly by shifting upper body
    f1 = shift_pixels(idle, 0, -1)
    f2 = idle.copy()
    f2_d = ImageDraw.Draw(f2)
    # tail flick
    f2_d.line([(14, 42), (11, 38), (10, 35)], fill=COLOR_FUR_MAIN, width=3)
    f2_d.point([(10, 35)], fill=COLOR_OUTLINE)
    f3 = shift_pixels(idle, 0, 0)
    save_strip([f0, f1, f2, f3], "Idle.png")

    # 2. Sit (4 frames: sitting down, calm breathing, tail curled around paws)
    sit_frames = []
    for i in range(4):
        # Sitting cat body: body shifted down by 2px, paws tucked in
        sf = shift_pixels(idle, 0, 2)
        d = ImageDraw.Draw(sf)
        # draw curled tail wrapping around bottom paws
        d.line([(18, 54), (24, 56), (32, 56), (40, 55)], fill=COLOR_FUR_SHADOW, width=2)
        d.line([(18, 53), (24, 55), (32, 55), (40, 54)], fill=COLOR_FUR_MAIN, width=2)
        if i == 1 or i == 2:
            # subtle breathing
            sf = shift_pixels(sf, 0, -1)
        if i == 3:
            # calm eye blink
            d.line([(22, 33), (25, 33)], fill=COLOR_OUTLINE, width=1)
            d.line([(34, 33), (37, 33)], fill=COLOR_OUTLINE, width=1)
        sit_frames.append(sf)
    save_strip(sit_frames, "Sit.png")

    # 3. WakeUp (5 frames: sleep -> crack eyes -> yawn stretch -> rise -> idle alert)
    wu_frames = []
    # frame 0: asleep
    wu_frames.append(sleep1.copy())
    # frame 1: eyes crack open
    f1 = sleep0.copy()
    d1 = ImageDraw.Draw(f1)
    d1.point([(34, 43), (44, 43)], fill=COLOR_EYE)
    wu_frames.append(f1)
    # frame 2: front stretch arch (paws forward, body low)
    f2 = shift_pixels(sleep0, -2, 1)
    d2 = ImageDraw.Draw(f2)
    d2.line([(45, 48), (55, 54)], fill=COLOR_FUR_MAIN, width=3) # stretching paw
    d2.line([(45, 47), (55, 53)], fill=COLOR_CREAM, width=2)
    wu_frames.append(f2)
    # frame 3: rising up
    f3 = shift_pixels(idle, 0, 3)
    wu_frames.append(f3)
    # frame 4: fully standing alert
    wu_frames.append(idle.copy())
    save_strip(wu_frames, "WakeUp.png")

    # 4. Drink (5 frames: lap bowl -> tongue lap -> droplets -> lick nose -> smile)
    drink_frames = []
    for i in range(5):
        df = shift_pixels(idle, 0, 2 if i < 3 else 0)
        d = ImageDraw.Draw(df)
        # Saucer/bowl on floor at (36, 52)
        d.ellipse([(38, 50), (56, 56)], fill=(200, 230, 255, 255), outline=COLOR_OUTLINE)
        d.ellipse([(40, 51), (54, 54)], fill=(255, 255, 255, 255)) # milk
        if i == 1 or i == 2:
            # tongue out lapping milk
            d.polygon([(34, 44), (42, 50), (38, 50)], fill=COLOR_PINK)
            # tiny water droplets
            d.point([(43, 47), (47, 46)], fill=COLOR_WATER)
        elif i == 3:
            # paw licking whiskers
            d.line([(32, 40), (36, 36)], fill=COLOR_CREAM, width=3)
        elif i == 4:
            # happy closed eyes
            d.line([(22, 33), (25, 33)], fill=COLOR_OUTLINE, width=1)
            d.line([(34, 33), (37, 33)], fill=COLOR_OUTLINE, width=1)
            d.point([(29, 35)], fill=COLOR_PINK) # little nose lick
        drink_frames.append(df)
    save_strip(drink_frames, "Drink.png")

    # 5. Play (6 frames: crouch -> pounce with ball -> ball bounces -> bat ball -> trap -> grin)
    play_frames = []
    for i in range(6):
        pf = shift_pixels(happy0 if i in [2, 3] else walk0, 0, 1 if i in [0, 4] else -2)
        d = ImageDraw.Draw(pf)
        # Toy ball coordinates per frame
        ball_pos = [
            (48, 52),  # frame 0: ball on ground in front
            (45, 46),  # frame 1: bat ball forward
            (42, 32),  # frame 2: ball bounces high
            (38, 28),  # frame 3: swat ball at apex
            (34, 48),  # frame 4: trap ball between paws
            (36, 50)   # frame 5: hold ball proudly
        ]
        bx, by = ball_pos[i]
        d.ellipse([(bx - 4, by - 4), (bx + 4, by + 4)], fill=COLOR_BALL, outline=COLOR_OUTLINE)
        d.point([(bx - 1, by - 1)], fill=COLOR_BALL_YELLOW)
        play_frames.append(pf)
    save_strip(play_frames, "Play.png")

    # 6. Dance (6 frames: 2-legged sway left -> head bob -> sway right -> hop twirl -> pose)
    dance_frames = []
    for i in range(6):
        dx_shift = [-2, -1, 2, 1, 0, 0][i]
        dy_shift = [-1, -3, -1, -4, -2, 0][i]
        df = shift_pixels(happy1 if i in [1, 3] else happy0, dx_shift, dy_shift)
        d = ImageDraw.Draw(df)
        # Musical note sparkles around dancer
        if i in [1, 2]:
            d.point([(14, 22), (15, 23), (16, 22), (16, 20)], fill=(171, 71, 188, 255))
        elif i in [4, 5]:
            d.point([(48, 18), (49, 19), (50, 18), (50, 16)], fill=(255, 179, 0, 255))
        dance_frames.append(df)
    save_strip(dance_frames, "Dance.png")

    # 7. Jump (5 frames: deep crouch -> launch up -> apex -> descending -> landing)
    jump_frames = []
    # Frame 0: crouch
    jf0 = shift_pixels(idle, 0, 3)
    jump_frames.append(jf0)
    # Frame 1: launch
    jf1 = shift_pixels(happy0, 0, -5)
    jump_frames.append(jf1)
    # Frame 2: apex
    jf2 = shift_pixels(happy1, 0, -10)
    jump_frames.append(jf2)
    # Frame 3: descending
    jf3 = shift_pixels(walk2, 0, -4)
    jump_frames.append(jf3)
    # Frame 4: landing cushion
    jf4 = shift_pixels(idle, 0, 2)
    jump_frames.append(jf4)
    save_strip(jump_frames, "Jump.png")

    # 8. Fall (4 frames: flailing paws mid-air, rotation tilt)
    fall_frames = []
    for i in range(4):
        tilt_angle = [-8, 8, -6, 6][i]
        ff = shift_pixels(hurt0, 0, -2).rotate(tilt_angle, resample=Image.NEAREST)
        d = ImageDraw.Draw(ff)
        # Wind rushing lines
        d.line([(12, 14), (12, 22)], fill=(255, 255, 255, 180), width=1)
        d.line([(52, 16), (52, 26)], fill=(255, 255, 255, 180), width=1)
        fall_frames.append(ff)
    save_strip(fall_frames, "Fall.png")

    # 9. Dirty (4 frames: mud splatters, shaking off dirt, grimy sigh)
    dirty_frames = []
    for i in range(4):
        df = shift_pixels(idle, [-1, 1, -1, 0][i], 0).copy()
        d = ImageDraw.Draw(df)
        # Mud patches on cheek, body, and paws
        d.ellipse([(18, 36), (22, 39)], fill=COLOR_DIRT_DARK)
        d.ellipse([(38, 38), (43, 42)], fill=COLOR_DIRT)
        d.ellipse([(28, 46), (34, 50)], fill=COLOR_DIRT_DARK)
        d.ellipse([(44, 52), (48, 55)], fill=COLOR_DIRT)
        if i in [1, 2]:
            # dirt puff / flying dust specks
            d.point([(14, 32), (16, 30), (46, 34), (49, 31)], fill=COLOR_DIRT)
        dirty_frames.append(df)
    save_strip(dirty_frames, "Dirty.png")

    # 10. Sick (4 frames: curled, dizzy spiral eyes, damp forehead compress)
    sick_frames = []
    for i in range(4):
        sf = shift_pixels(sleep0, 0, 1).copy()
        d = ImageDraw.Draw(sf)
        # Forehead cooling ice pack / compress
        d.polygon([(36, 28), (46, 26), (44, 32), (34, 34)], fill=COLOR_SICK_ICE, outline=COLOR_OUTLINE)
        # Dizzy spiral / tired expression
        if i % 2 == 0:
            d.point([(38, 40), (44, 40)], fill=(74, 20, 140, 255))
        else:
            d.point([(39, 41), (45, 41)], fill=(74, 20, 140, 255))
        # Pale shivering tint
        sick_frames.append(sf)
    save_strip(sick_frames, "Sick.png")

    # 11. Sad (4 frames: folded ears, drooping head, tear rolling down cheek)
    sad_frames = []
    for i in range(4):
        sf = shift_pixels(idle, 0, 2).copy()
        d = ImageDraw.Draw(sf)
        # Drooping sad eyes
        d.line([(22, 34), (25, 36)], fill=COLOR_OUTLINE, width=2)
        d.line([(34, 36), (37, 34)], fill=COLOR_OUTLINE, width=2)
        # Teardrop
        tear_y = [36, 39, 42, 45][i]
        d.ellipse([(20, tear_y), (23, tear_y + 3)], fill=COLOR_WATER, outline=COLOR_WATER_LIGHT)
        sad_frames.append(sf)
    save_strip(sad_frames, "Sad.png")

    # 12. Angry (4 frames: bristling fur, airplane ears, paw stomp, angry steam)
    angry_frames = []
    for i in range(4):
        af = shift_pixels(idle, [0, 1, 0, -1][i], 0).copy()
        d = ImageDraw.Draw(af)
        # Slanted sharp angry eyebrows
        d.line([(21, 31), (25, 33)], fill=COLOR_OUTLINE, width=2)
        d.line([(34, 33), (38, 31)], fill=COLOR_OUTLINE, width=2)
        # Angry vein symbol / steam puff
        if i in [1, 2]:
            # red angry cross mark
            d.line([(46, 20), (52, 26)], fill=COLOR_ANGER, width=2)
            d.line([(52, 20), (46, 26)], fill=COLOR_ANGER, width=2)
            # tiny steam puff from nose
            d.point([(29, 34), (30, 33), (31, 34)], fill=(255, 255, 255, 200))
        angry_frames.append(af)
    save_strip(angry_frames, "Angry.png")

    print("\n=== ALL CAT ANIMATION ASSETS GENERATED SUCCESSFULLY ===")

if __name__ == "__main__":
    generate_all()
