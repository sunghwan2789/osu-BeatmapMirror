SET SQL_MODE = "NO_AUTO_VALUE_ON_ZERO";
SET AUTOCOMMIT = 0;
START TRANSACTION;
SET time_zone = "+00:00";

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8mb4 */;


CREATE TABLE `gosu_beatmaps` (
  `setId` int(10) UNSIGNED NOT NULL,
  `id` int(10) UNSIGNED NOT NULL,
  `hash_sha2` char(64) COLLATE utf8mb4_unicode_ci NOT NULL,
  `hash_md5` char(32) COLLATE utf8mb4_unicode_ci NOT NULL,
  `name` tinytext COLLATE utf8mb4_unicode_ci NOT NULL,
  `author` tinytext COLLATE utf8mb4_unicode_ci NOT NULL,
  `mode` tinyint(3) NOT NULL,
  `cs` float UNSIGNED NOT NULL,
  `ar` float UNSIGNED NOT NULL,
  `od` float UNSIGNED NOT NULL,
  `hp` float UNSIGNED NOT NULL,
  `bpm` double UNSIGNED NOT NULL,
  `length` int(10) UNSIGNED NOT NULL,
  `star` double UNSIGNED NOT NULL,
  `status` tinyint(3) UNSIGNED NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `gosu_downloads` (
  `setId` int(10) UNSIGNED NOT NULL,
  `ip` int(10) UNSIGNED NOT NULL,
  `at` date NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci ROW_FORMAT=COMPRESSED;

CREATE TABLE `gosu_download_summary` (
  `setId` int(10) UNSIGNED NOT NULL,
  `date` date NOT NULL,
  `downloads` smallint(5) UNSIGNED NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci ROW_FORMAT=COMPACT;

CREATE TABLE `gosu_packs` (
  `id` varchar(10) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL,
  `title` tinytext COLLATE utf8mb4_unicode_ci NOT NULL,
  `synced` datetime(3) NOT NULL DEFAULT current_timestamp(3)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `gosu_pack_sets` (
  `packId` varchar(10) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL,
  `setId` int(10) UNSIGNED NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `gosu_sets` (
  `id` int(10) UNSIGNED NOT NULL,
  `artist` tinytext COLLATE utf8mb4_unicode_ci NOT NULL,
  `artistU` tinytext COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `title` tinytext COLLATE utf8mb4_unicode_ci NOT NULL,
  `titleU` tinytext COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `creatorId` int(10) UNSIGNED NOT NULL,
  `creator` tinytext COLLATE utf8mb4_unicode_ci NOT NULL,
  `genreId` tinyint(3) UNSIGNED NOT NULL,
  `languageId` tinyint(3) UNSIGNED NOT NULL,
  `source` tinytext COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `tags` text COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `status` tinyint(3) UNSIGNED NOT NULL,
  `rankedAt` datetime DEFAULT NULL,
  `synced` datetime(3) NOT NULL DEFAULT current_timestamp(3),
  `keyword` text COLLATE utf8mb4_unicode_ci NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `osu_custom_list` (
  `id` mediumint(8) UNSIGNED NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


ALTER TABLE `gosu_beatmaps`
  ADD PRIMARY KEY (`id`),
  ADD KEY `set` (`setId`),
  ADD KEY `hash_md5` (`hash_md5`),
  ADD KEY `status` (`status`),
  ADD KEY `mode` (`mode`);

ALTER TABLE `gosu_downloads`
  ADD PRIMARY KEY (`at`,`setId`,`ip`) USING BTREE,
  ADD KEY `at` (`at`);

ALTER TABLE `gosu_download_summary`
  ADD PRIMARY KEY (`setId`,`date`) USING BTREE,
  ADD KEY `date` (`date`,`downloads`);

ALTER TABLE `gosu_packs`
  ADD PRIMARY KEY (`id`),
  ADD KEY `synced` (`synced`);

ALTER TABLE `gosu_pack_sets`
  ADD KEY `packId` (`packId`),
  ADD KEY `setId` (`setId`);

ALTER TABLE `gosu_sets`
  ADD PRIMARY KEY (`id`),
  ADD KEY `rankedAt` (`rankedAt`,`synced`),
  ADD KEY `status` (`status`,`rankedAt`,`synced`);

ALTER TABLE `osu_custom_list`
  ADD PRIMARY KEY (`id`);


ALTER TABLE `gosu_beatmaps`
  ADD CONSTRAINT `gosu_beatmaps_ibfk_1` FOREIGN KEY (`setId`) REFERENCES `gosu_sets` (`id`) ON DELETE CASCADE ON UPDATE CASCADE;

ALTER TABLE `gosu_pack_sets`
  ADD CONSTRAINT `gosu_pack_sets_ibfk_1` FOREIGN KEY (`packId`) REFERENCES `gosu_packs` (`id`) ON DELETE CASCADE ON UPDATE CASCADE,
  ADD CONSTRAINT `gosu_pack_sets_ibfk_2` FOREIGN KEY (`setId`) REFERENCES `gosu_sets` (`id`) ON DELETE CASCADE ON UPDATE CASCADE;

COMMIT;

/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
