-- Public Workouts DB Tables
CREATE TABLE IF NOT EXISTS "PublicWorkouts" (
    "Id" UUID PRIMARY KEY,
    "PrivateWorkoutId" UUID,
    "AuthorId" UUID NOT NULL,
    "Name" TEXT NOT NULL,
    "Type" TEXT NOT NULL,
    "PreviewUrl" TEXT,
    "LikesCount" INTEGER DEFAULT 0,
    "CopiesCount" INTEGER DEFAULT 0,
    "CreatedAt" TIMESTAMP NOT NULL,
    "LastSyncedAt" TIMESTAMP
);

CREATE TABLE IF NOT EXISTS "Exercises" (
    "Id" UUID PRIMARY KEY,
    "WorkoutId" UUID NOT NULL REFERENCES "PublicWorkouts"("Id") ON DELETE CASCADE,
    "ExerciseId" UUID NOT NULL,
    "Name" TEXT NOT NULL,
    "Description" TEXT,
    "MediaUrl" TEXT,
    "OrderIndex" INTEGER NOT NULL,
    "DurationSeconds" INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS "Likes" (
    "Id" SERIAL PRIMARY KEY,
    "WorkoutId" UUID NOT NULL REFERENCES "PublicWorkouts"("Id") ON DELETE CASCADE,
    "UserId" UUID NOT NULL,
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE("WorkoutId", "UserId")
);

CREATE TABLE IF NOT EXISTS "Copies" (
    "Id" SERIAL PRIMARY KEY,
    "WorkoutId" UUID NOT NULL REFERENCES "PublicWorkouts"("Id") ON DELETE CASCADE,
    "UserId" UUID NOT NULL,
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE("WorkoutId", "UserId")
);

CREATE TABLE IF NOT EXISTS "Comments" (
    "Id" SERIAL PRIMARY KEY,
    "WorkoutId" UUID NOT NULL REFERENCES "PublicWorkouts"("Id") ON DELETE CASCADE,
    "UserId" UUID NOT NULL,
    "Text" TEXT NOT NULL,
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS "CommentLikes" (
    "Id" SERIAL PRIMARY KEY,
    "CommentId" INTEGER NOT NULL REFERENCES "Comments"("Id") ON DELETE CASCADE,
    "UserId" UUID NOT NULL,
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE("CommentId", "UserId")
);
