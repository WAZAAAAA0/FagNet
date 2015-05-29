-- --------------------------------------------------------
-- Host:                         127.0.0.1
-- Server Version:               5.5.32 - MySQL Community Server (GPL)
-- Server Betriebssystem:        Win32
-- HeidiSQL Version:             8.0.0.4396
-- --------------------------------------------------------

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET NAMES utf8 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;

-- Exportiere Struktur von Tabelle auth.accounts
CREATE TABLE IF NOT EXISTS `accounts` (
  `ID` bigint(20) unsigned NOT NULL AUTO_INCREMENT,
  `Username` varchar(12) COLLATE utf8_bin NOT NULL DEFAULT '',
  `Nickname` varchar(12) COLLATE utf8_bin NOT NULL DEFAULT '',
  `Password` varchar(90) COLLATE utf8_bin NOT NULL DEFAULT '',
  `Banned` bigint(20) unsigned NOT NULL DEFAULT '0',
  `BanReason` varchar(1024) COLLATE utf8_bin NOT NULL DEFAULT '',
  `GMLevel` tinyint(3) unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`ID`),
  UNIQUE KEY `Username` (`Username`),
  UNIQUE KEY `ID` (`ID`),
  UNIQUE KEY `Nickname` (`Nickname`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_bin;

-- Daten Export vom Benutzer nicht ausgewählt


-- Exportiere Struktur von Tabelle auth.server
CREATE TABLE IF NOT EXISTS `server` (
  `UID` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `ID` smallint(5) unsigned NOT NULL DEFAULT '1',
  `Type` tinyint(1) unsigned NOT NULL DEFAULT '1',
  `Name` varchar(39) COLLATE utf8_bin NOT NULL,
  `PlayerLimit` smallint(5) unsigned NOT NULL DEFAULT '0',
  `IP` varchar(16) COLLATE utf8_bin NOT NULL DEFAULT '127.0.0.1',
  `Port` smallint(5) unsigned NOT NULL DEFAULT '28008',
  PRIMARY KEY (`UID`),
  UNIQUE KEY `Name` (`Name`),
  UNIQUE KEY `UID` (`UID`),
  UNIQUE KEY `Port` (`Port`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_bin;

-- Daten Export vom Benutzer nicht ausgewählt
/*!40101 SET SQL_MODE=IFNULL(@OLD_SQL_MODE, '') */;
/*!40014 SET FOREIGN_KEY_CHECKS=IF(@OLD_FOREIGN_KEY_CHECKS IS NULL, 1, @OLD_FOREIGN_KEY_CHECKS) */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
