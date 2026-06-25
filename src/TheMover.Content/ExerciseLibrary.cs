// TheMover.Content — bundled exercise catalog (>= 10 exercises, ADR-0003)
namespace TheMover.Content;

public static class ExerciseLibrary
{
    // Collection expression compiles to Exercise[] which is truly immutable: casting to
    // IList<T> yields IsReadOnly=true and Add/Remove throw NotSupportedException.
    // A List<Exercise> would let callers corrupt the catalog via a downcast.
    public static readonly IReadOnlyList<Exercise> All =
    [
        new("neck-roll",       "Neck Roll",            "Slowly roll your head from right to left and back again. Complete 3 slow circles in each direction."),
        new("shoulder-shrug",  "Shoulder Shrug",       "Raise both shoulders to your ears, hold for 3 seconds, then release. Repeat 5 times."),
        new("eye-focus",       "20-20-20 Eye Rest",    "Look at an object at least 20 feet away for 20 seconds. Blink softly 20 times to re-moisturise."),
        new("wrist-circles",   "Wrist Circles",        "Extend both arms and make large slow circles with your wrists, 5 times clockwise then 5 counterclockwise."),
        new("chest-opener",    "Chest Opener",         "Clasp your hands behind your back. Gently squeeze your shoulder blades together and lift your hands slightly. Hold 15 seconds."),
        new("spinal-twist",    "Seated Spinal Twist",  "Sit tall and place your right hand on your left knee. Twist gently to the left and look over your shoulder. Hold 15 seconds each side."),
        new("hip-flexor",      "Hip Flexor Stretch",   "Stand up, step one foot forward into a lunge, and sink your hips toward the floor. Hold 20 seconds then switch legs."),
        new("calf-raise",      "Calf Raise",           "Stand beside your desk and rise up onto your toes. Hold 2 seconds at the top, then lower slowly. Repeat 10 times."),
        new("wrist-stretch",   "Wrist & Forearm",      "Extend one arm in front, palm up. Use your other hand to gently pull your fingers back. Hold 15 seconds, then switch."),
        new("box-breathing",   "Box Breathing",        "Inhale slowly for 4 counts, hold for 4, exhale for 4, hold for 4. Repeat the cycle 4 times."),
        new("upper-back-row",  "Upper Back Row",       "Extend your arms forward at shoulder height, then pull your elbows back while squeezing your shoulder blades together. Repeat 10 times."),
        new("finger-spread",   "Finger Spread",        "Spread your fingers as wide as possible and hold for 5 seconds. Make a loose fist and hold for 5 seconds. Repeat 5 times."),
    ];
}
